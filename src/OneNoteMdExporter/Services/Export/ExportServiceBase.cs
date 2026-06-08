using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Base class for Export Service. 
    /// Contains all shared logic between exporter of different formats.
    /// Abstract methods needs to be implemented by each exporter
    /// </summary>
    public abstract class ExportServiceBase : IExportService
    {
        protected abstract string ExportFormatCode { get; }

        protected static string GetNotebookFolderPath(Notebook notebook)
            => Path.Combine(notebook.ExportFolder, notebook.GetNotebookPath());

        /// <summary>
        /// Return location in the export folder of an attachment file
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId">Id of the attachment</param>
        /// <param name="oneNoteFilePath">Original file path of the file in OneNote</param>
        /// <returns></returns>
        protected abstract string GetAttachmentFilePath(Attachement attachment);

        /// <summary>
        /// Get the md reference to the attachment
        /// </summary>
        /// <param name="attachment"></param>
        /// <returns></returns>
        protected abstract string GetAttachmentMdReference(Attachement attachment);

        protected abstract string GetResourceFolderPath(Page node);

        protected abstract string GetPageMdFilePath(Page page);


        public NotebookExportResult ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            notebook.ExportFolder = @$"{Localizer.GetString("ExportFolder")}\{ExportFormatCode}\{notebook.GetNotebookPath()}-{DateTime.Now:yyyyMMdd HH-mm}";
            CleanUpFolder(notebook);

            // Initialize hierarchy of the notebook from OneNote APIs
            try
            {
                OneNoteApp.Instance.FillNodebookTree(notebook);
            }
            catch (Exception ex)
            {
                return new NotebookExportResult
                {
                    NoteBookExportErrorCode = "ErrorDuringNotebookProcessingNbTree",
                    NoteBookExportErrorMessage = string.Format(Localizer.GetString("ErrorDuringNotebookProcessingNbTree"),
                        notebook.Title, notebook.Id, ex.Message)
                };
            }

            return ExportNotebookInTargetFormat(notebook, sectionNameFilter, pageNameFilter);
        }

        public abstract NotebookExportResult ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "");

        private static void CleanUpFolder(Notebook notebook)
        {
            // Cleanup Notebook export folder
            DirectoryHelper.ClearFolder(GetNotebookFolderPath(notebook));

            // Cleanup temp folder
            DirectoryHelper.ClearFolder(GetTmpFolder(notebook));
        }

        protected abstract void PrepareFolders(Page page);

        protected static string GetTmpFolder(Node node)
            => Path.Combine(Path.GetTempPath(), node.GetNotebookPath());

        /// <summary>
        /// Export a Page and its attachments
        /// </summary>
        /// <param name="page"></param>
        /// <param name="retry">True if the execution is caused by a retry after an error on the page</param>
        /// <returns>True if the export finished with success</returns>
        protected bool ExportPage(Page page, bool retry = false)
        {
            try
            {
                OneNoteApp.Instance.GetPageContent(page.OneNoteId, out var xmlPageContentStr, PageInfo.piBinaryDataFileType);

                // Alternative : return page content without binaries
                //oneNoteApp.GetHierarchy(page.OneNoteId, HierarchyScope.hsChildren, out var xmlAttach);

                var xmlPageContent = XDocument.Parse(xmlPageContentStr).Root;
                var ns = xmlPageContent.Name.Namespace;
                page.Author = xmlPageContent.Element(ns + "Title")?.Element(ns + "OE")?.Attribute("author")?.Value ?? "unknown";
                ProcessPageAttachments(ns, page, xmlPageContent);

                // Suffix page title
                EnsurePageUniquenessPerSection(page);

                if (!AppSettings.DisablePageXmlPreProcessing)
                {
                    // Make various OneNote XML fixes before page export
                    page.OverrideOneNoteId = PageXmlPreProcessing(xmlPageContent);
                }

                // Register page and section mappings for link conversion
                var pagePath = page.GetPageFileAbsolutePath(AppSettings.MdMaxFileLength);

                // Generate programmatic ID for the page (OneNote links replacement feature
                var oneNoteLinkTranslatorService = new OneNoteLinkTranslatorService();
                oneNoteLinkTranslatorService.initializePage(page, pagePath);


                var docxFileTmpFile = Path.Combine(GetTmpFolder(page), page.Id + ".docx");

                if (File.Exists(docxFileTmpFile))
                    File.Delete(docxFileTmpFile);

                PrepareFolders(page);

                Log.Debug($"{page.OneNoteId}: start OneNote docx publish");
                if (page.OverrideOneNoteId != null)
                    Log.Debug($"Actually using temporary page ${page.OverrideOneNoteId}");

                // Request OneNote to export the page into a DocX file
                OneNoteApp.Instance.Publish(page.OverrideOneNoteId ?? page.OneNoteId, Path.GetFullPath(docxFileTmpFile), PublishFormat.pfWord);

                Log.Debug($"{page.OneNoteId}: success");

                if (AppSettings.Debug || AppSettings.KeepOneNoteTempFiles)
                {
                    // If debug mode enabled, copy the page docx file next to the page md file
                    var docxFilePath = Path.ChangeExtension(GetPageMdFilePath(page), "docx");
                    File.Copy(docxFileTmpFile, docxFilePath);
                }

                // Convert docx file into Md using PanDoc
                var pageMd = ConverterService.ConvertDocxToMd(page, docxFileTmpFile, GetTmpFolder(page));

                if (AppSettings.Debug)
                {
                    // And write Pandoc markdown file
                    var mdPanDocFilePath = Path.ChangeExtension(GetPageMdFilePath(page), "pandoc.md");
                    File.WriteAllText(mdPanDocFilePath, pageMd);
                }

                File.Delete(docxFileTmpFile);

                // Copy images extracted from DocX to Export folder and add them in the list of attachments of the page
                try
                {
                    ExtractImagesToResourceFolder(page, ref pageMd);
                }
                catch (COMException ex)
                {
                    if (ex.Message.Contains("0x800706BE"))
                    {
                        LogError(page, ex, Localizer.GetString("ErrorWhileStartingOnenote"));
                    }
                    else
                        LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringOneNoteExport"), ex.Message));
                }
                catch (Exception ex)
                {
                    LogError(page, ex, Localizer.GetString("ErrorImageExtract"));
                }

                // Export all file attachments and get updated page markdown including md reference to attachments
                ExportPageAttachments(page, ref pageMd);

                // Apply post processing to Page Md content
                ConverterService.PageMdPostConversion(ref pageMd);

                // Convert OneNote:// links to markdown links
                pageMd = oneNoteLinkTranslatorService.ConvertOneNoteLinks(pageMd, GetPageWikilink);

                // Apply post processing specific to an export format
                pageMd = FinalizePageMdPostProcessing(page, pageMd);

                WritePageMdFile(page, pageMd);

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("0x800706BE"))
                {
                    LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringPageProcessingIsOneNoteRunning"), page.TitleWithPageLevelTabulation, page.Id, ex.Message));
                }
                else if (ex.Message.Contains("0x800706BA")) // Server RPC not available, occurs after a crash of OneNote
                {
                    if (!retry)
                    {
                        // 1st attempt, reinit OneNote connector and make a 2nd try

                        var delayBeforeRetrySeconds = 10;
                        LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringPageProcessingRetryInProgress"), page.TitleWithPageLevelTabulation, page.Id, ex.Message, delayBeforeRetrySeconds));

                        // Recreate OneNote COM component to avoid "Server RPC not available" errors
                        OneNoteApp.CleanUp();
                        Thread.Sleep(delayBeforeRetrySeconds * 1000);
                        OneNoteApp.RenewInstance();

                        var retrySuccess = ExportPage(page, true);
                        if (retrySuccess)
                        {
                            Log.Information($"Page '{page.GetPageFileRelativePath(AppSettings.MdMaxFileLength)}': {Localizer.GetString("SuccessPageExportAfterRetry")}");
                            return true;
                        }
                        else
                            LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, ex.Message));
                    }
                    else
                    {
                        LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, ex.Message));
                    }
                }
                else
                {
                    LogError(page, ex, string.Format(Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, ex.Message));
                }

                return false;
            }
        }

        protected abstract string GetPageWikilink(string linkText, string mdFilePath, string pageId);

        /// <summary>
        /// Pre-process OneNote XML page for: Sections unfold, Convert OneNote tags to #hash-tags, Keep checkboxes, etc.
        //  To avoid any modification on OneNote original page, if page XML content was changed due pre-processing 
        //  the modified page is stored in a temporary notebook that will be used to export page in replacement to the original page
        // /!\ Copy a page in the temporary notebook is resource intensive and should be avoided when possible /!\
        /// </summary>
        /// <param name="xmlPageContent">Page to pre-process</param>
        /// <returns>Temporary OneNote ID of changed pre-processed page or NULL if pre-processing do not changed page XML</returns>
        private string PageXmlPreProcessing(XElement xmlPageContent)
        {
            // Trigger on any XML tree changes so we know that this page should be cloned to temporary notebook
            var isXmlChanged = false;
            void ChangesHandler(object _, XObjectChangeEventArgs __)
            {
                isXmlChanged = true;
                xmlPageContent.Changed -= ChangesHandler;
            }
            xmlPageContent.Changed += ChangesHandler;

            var ns = xmlPageContent.Name.Namespace;

            /// Unfold page content by removing all OneNote XML attribute "collapsed" everywhere
            foreach (var xmlOutline in xmlPageContent.Descendants(ns + "OE"))
            {
                xmlOutline.Attribute("collapsed")?.Remove();
            }

           /// Keep "OneNote tag information" by adding custom tags in text content
            ConvertOnenoteTags(xmlPageContent, ns);

            /// Make indenting explicit in content by adding empty lines before text blocks
            /// NB: this has to be AFTER the ConvertOnenoteTags method, otherwise the tabs come in between the tags and the text
            if (AppSettings.IndentingStyle != IndentingStyleEnum.LeaveAsIs)
                ConvertIndentation(xmlPageContent, ns, AppSettings.IndentingStyle);

            /// Add horizontal bar before text blocks
            AddHorizontalBarBeforeTextblocks(xmlPageContent, ns);

            if (AppSettings.UseHtmlStyling) /// Keep HTML highlighting (using span elements)
            {
                // Escape HTML span elements for style attributes not handled by pandoc / not supported by markdown
                EscapeStylingSpan(xmlPageContent, ns);
            }

            if (isXmlChanged)
                return TemporaryNotebook.ClonePage(xmlPageContent);
            else
                return null;
        }

        /// <summary>
        /// Escape html tag not translated in markdown by PanDoc and that should be 
        /// included in markdown output
        /// </summary>
        /// <param name="xmlPageContent"></param>
        /// <param name="ns"></param>
        private static void EscapeStylingSpan(XElement xmlPageContent, XNamespace ns)
        {
            var styleRegexSearchStr = @"\s*[a-zA-Z0-9\s\.\#;:-]*((?:color|background)[^']*)";
            var styleRegexReplaceStr = @"\s*([^']*)";

            var htmlRegex = new Regex(@"<span\s+style='" + styleRegexSearchStr + @"'>(.*?)<\/span>");
            var styleRegex = new Regex(styleRegexSearchStr);

            foreach (var xmlText in xmlPageContent.Descendants(ns + "T"))
            {
                if (xmlText.FirstNode is not XCData cdataNode)
                {
                    // Only log if the tag is one we expect to handle
                    Log.Warning($"Found T-element but no CDATA-element, with Value: '{xmlText?.Value}'");
                    continue;
                }
                XCData innerNode = xmlText.FirstNode as XCData;
                var styleAttribute = xmlText.Attribute("style") ?? xmlText.Parent?.Attribute("style");

                // Case 1 - Style attribute is defined in a span html tag => Escape span tag
                if (htmlRegex.IsMatch(innerNode.Value))
                {

                    var htmlRegexReplace = new Regex(@"<span\s+style='" + styleRegexReplaceStr + @"'>(.*?)<\/span>");

                    innerNode.Value = htmlRegexReplace.Replace(innerNode.Value, match =>
                    {
                        // Remove \n in style tag to prevent PanDoc to replace them by <BR /> tags
                        return $"«span style='{match.Groups[1].ToString().Replace('\n', ' ')}'»{match.Groups[2]}«/span»";
                    });
                }
                // Case 2 - Style attribute is defined in the parent html tag => Add span tag 
                else if (styleRegex.IsMatch(styleAttribute?.Value ?? ""))
                {
                    var match = styleRegex.Match(styleAttribute?.Value ?? "");
                    if (match.Success)
                    {
                        // Remove \n in style tag to prevent PanDoc to replace them by <BR /> tags
                        var newValue = $"«span style='{match.Groups[1].ToString().Replace('\n', ' ') }'»{innerNode.Value}«/span»";
                        innerNode.Value = newValue;
                    };
                }
            }
        }


        private static readonly string HorizontalBar = "---" + Environment.NewLine + Environment.NewLine;
        private void AddHorizontalBarBeforeTextblocks(XElement xmlPageContent, XNamespace ns)
        {
            // Skip the first outline element
            foreach (var outline in xmlPageContent.Descendants(ns + "Outline").Skip(1))
            {
                // Find the first <T> element with a CDATA node (to be sure)
                var textElement = outline
                    .Descendants(ns + "T")
                    .FirstOrDefault(e => e.LastNode != null && e.LastNode.NodeType.ToString() == "CDATA");

                if (textElement == null)
                    continue;

                // Add a new line with the horizontal bar before the text element
                var emptyLineXml = new XElement(ns + "OE", new XAttribute("alignment", "left"),
                    new XElement(ns + "T", 
                    new XCData($"{HorizontalBar}")));
                textElement.Parent?.Parent?.AddFirst(emptyLineXml);
            }
        }

        const int EmSpacesPerIndent = 2;
        private void ConvertIndentation(XElement xmlPageContent, XNamespace ns, IndentingStyleEnum indentStyle)
        {
            string defaultFontSize = getQuickStyleFontsize(xmlPageContent, ns);
            foreach (var textElement in xmlPageContent.Descendants(ns + "T"))
            {
                // Determine indentation level and skip if not indented
                int indentLevel = textElement.Ancestors(ns + "OEChildren").Count() - 1;
                if (indentLevel <= 0)
                    continue;

                // If already a list, we can skip it
                var prevEl = textElement.PreviousNode as XElement;
                if (prevEl?.Name.LocalName == "List")
                    continue;

                // If inside a table, we skip it
                if (textElement.Ancestors(ns + "Table").Count() > 0)
                    continue;

                // TODO: check of in tabel!
                switch (indentStyle)
                {
                    case IndentingStyleEnum.ConvertToEmSpaces:
                        textElement.Value = Repeat("&emsp;", indentLevel * EmSpacesPerIndent) + textElement.Value;
                        break;
                    case IndentingStyleEnum.ConvertToBullets:
                        var bulletList = CreateListElement(ns, indentLevel, defaultFontSize);
                        textElement.AddBeforeSelf(bulletList);
                        break;
                }
            }
        }

        // Create list element with bullet:
        //      <one:List>
        //          <one:Bullet bullet = "13" fontSize = "11.0"/>
        //      </one:List>
        private XElement CreateListElement(XNamespace ns, int indentLevel, string fontSize)
        {
            return new XElement(ns + "List",
                new XElement(ns + "Bullet",
                    new XAttribute("bullet", indentLevel.ToString()),
                    new XAttribute("fontSize", fontSize)));
        }

        public string Repeat(string text, int n)
        {
            var textAsSpan = text.AsSpan();
            var span = new Span<char>(new char[textAsSpan.Length * n]);
            for (var i = 0; i < n; i++)
            {
                textAsSpan.CopyTo(span.Slice((int)i * textAsSpan.Length, textAsSpan.Length));
            }

            return span.ToString();
        }

        private Dictionary<string, string[]> GetTagDefDict(XElement xmlPageContent, XNamespace ns) {
            // Get all tag definitions from the page content
            Dictionary<string, string[]> tags = [];

            foreach (var tagDef in xmlPageContent.Descendants(ns + "TagDef"))
            {
                if (!string.IsNullOrEmpty(tagDef.Attribute("symbol")?.Value) && !string.IsNullOrEmpty(tagDef.Attribute("index")?.Value))
                {
                    if(TagsDefMap.Map.TryGetValue(tagDef.Attribute("symbol")?.Value, out var value))
                    {
                        tags[tagDef.Attribute("index")?.Value] = value;
                    }
                    else
                    {
                        Log.Debug("GetTagDefDict: tag symbol " + tagDef.Attribute("symbol")?.Value + " ("+ tagDef.Attribute("name")?.Value + ") not found");
                    }

                }
            }
            return tags;
        }



        /// <summary>
        /// Convert Onenote tags to custom tags/emoticons in the text content so the tag information is conveyed to end result.
        /// In theory you could try and replace the custom tags with markdown compatible elements (e.g. for tasks), but this has too many edge cases (e.g. task in table).
        /// If you want to do this, you could use the "FinalizePageMdPostProcessing" method for this.
        /// </summary>
        /// <param name="xmlPageContent"></param>
        /// <param name="ns"></param>
        private void ConvertOnenoteTags(XElement xmlPageContent, XNamespace ns)
        {
            var tagsDef = GetTagDefDict(xmlPageContent, ns);

            // Find occurances and replace
            foreach (var tagElement in xmlPageContent.Descendants(ns + "Tag"))
            {
                // Get the corresponding text element
                XElement textElement = tagElement.Parent.Descendants(ns + "T").First() as XElement;
                if (textElement.FirstNode is not XCData)
                {
                    Log.Warning($"Found tag, but couldn't add custom tag. No CDATA-field found for element with content: '{textElement?.Value}'");
                    continue;
                }

                var tagIndex = tagElement.Attribute("index")?.Value;
                if (!tagsDef.ContainsKey(tagIndex))
                    continue;

                // Determine which custom tag to use
                var customTagSymbols = tagsDef[tagElement.Attribute("index")?.Value];
                string customTag = customTagSymbols.Length > 1 && tagElement.Attribute("completed")?.Value == "true" ? customTagSymbols[1] : customTagSymbols[0];

                // Add custom tag right before the tasks inner content
                XCData innerNode = textElement.FirstNode as XCData;
                var endtag = customTag == "==" ? "==" : "";
                var spacer = customTag == "==" ? "" : " ";

                innerNode.Value = $"{customTag}{spacer}{innerNode.Value}{endtag}";
                if (!innerNode.Value.EndsWith("\n&nbsp;\n")) innerNode.Value += "\n&nbsp;\n";
            }
        }

        private static string getQuickStyleFontsize(XElement xmlPageContent, XNamespace ns)
        {
            return getElementAttributeValue(xmlPageContent, ns, "QuickStyleDef", "p", "fontSize", "11.0");
        }
        private static string getElementAttributeValue(XElement xmlPageContent, XNamespace ns, string elementLabel, string elementName, string attributeLabel, string defaultValue)
        {
            return xmlPageContent
                .Descendants(ns + elementLabel)
                .FirstOrDefault(e => e.Attribute("name")?.Value == elementName)
                ?.Attribute(attributeLabel)?.Value ?? defaultValue;
        }

        protected abstract string FinalizePageMdPostProcessing(Page page, string md);

        private static void LogError(Page p, Exception ex, string message)
        {
            Log.Warning($"Page '{p.GetPageFileRelativePath(AppSettings.MdMaxFileLength)}': {message}");
            Log.Debug(ex, ex.Message);
        }

        /// <summary>
        /// Final class needs to implement logic to write the md file of the page in the export folder
        /// </summary>
        /// <param name="page">The page</param>
        /// <param name="pageMd">Markdown content of the page</param>
        protected abstract void WritePageMdFile(Page page, string pageMd);


        /// <summary>
        /// Create attachment files in export folder, and update page's markdown to insert md reference that link to the attachment files
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageMdFileContent">Markdown content of the page</param>
        private void ExportPageAttachments(Page page, ref string pageMdFileContent)
        {
            foreach (Attachement attach in page.Attachements)
            {
                if (attach.Type == AttachementType.File)
                {
                    EnsureAttachmentFileIsNotUsed(page, attach);

                    var exportFilePath = GetAttachmentFilePath(attach);

                    Directory.CreateDirectory(Path.GetDirectoryName(exportFilePath));

                    // Copy attachment file into export folder
                    File.Copy(attach.ActualSourceFilePath, exportFilePath);
                    //File.SetAttributes(exportFilePath, FileAttributes.Normal); // Prevent exception during removing of export directory

                    // Update page markdown to insert md references to attachments
                    InsertPageMdAttachmentReference(ref pageMdFileContent, attach, GetAttachmentMdReference);
                }

                FinalizeExportPageAttachments(page, attach);
            }
        }


        /// <summary>
        /// Final class needs to implement logic to write the md file of the attachment file in the export folder (if needed)
        /// </summary>
        /// <param name="page">The page</param>
        /// <param name="attachment">The attachment</param>
        protected abstract void FinalizeExportPageAttachments(Page page, Attachement attachment);


        /// <summary>
        /// Replace the tag <<FileName>> generated by OneNote by a markdown link referencing the attachment
        /// </summary>
        /// <param name="pageMdFileContent"></param>
        /// <param name="attach"></param>
        private static void InsertPageMdAttachmentReference(ref string pageMdFileContent, Attachement attach, Func<Attachement, string> getAttachMdReferenceMethod)
        {
            var pageMdFileContentModified = Regex.Replace(pageMdFileContent, "(\\\\<){2}(?<fileName>.*)(\\\\>){2}", delegate (Match match)
            {
                var refFileName = match.Groups["fileName"]?.Value ?? "";
                var attachOriginalFileName = attach.OneNotePreferredFileName;
                var attachMdRef = getAttachMdReferenceMethod(attach);

                if (refFileName.Equals(attachOriginalFileName))
                {
                    // reference found is corresponding to the attachment being processed
                    return $"[{attachOriginalFileName}]({attachMdRef})";
                }
                else
                {
                    // not the current attachment, ignore
                    return match.Value;
                }
            });

            pageMdFileContent = pageMdFileContentModified;
        }


        /// <summary>
        /// Replace PanDoc IMG HTML tag by markdown reference and copy image file into notebook export directory
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent">Content of the MD file</param>
        /// <param name="resourceFolderPath">The path to the notebook folder where store attachments</param>
        public void ExtractImagesToResourceFolder(Page page, ref string mdFileContent)
        {
            
            string processImgTag(string tag, bool outputHtmlTag)
            {
                // http://regexstorm.net/tester
                string regexImgAttributes = "<img src=\"(?<src>[^\"]+)\".* />";

                MatchCollection matches = Regex.Matches(tag, regexImgAttributes, RegexOptions.IgnoreCase);
                Match imgMatch = matches[0];

                var panDocHtmlImgTagPath = Path.GetFullPath(imgMatch.Groups["src"].Value);
                panDocHtmlImgTagPath = WebUtility.HtmlDecode(panDocHtmlImgTagPath);

                // Convert Windows metafile formats (EMF/WMF) to PNG for markdown viewer compatibility
                panDocHtmlImgTagPath = ConvertMetafileToImageIfNeeded(panDocHtmlImgTagPath);

                Attachement imgAttach = page.ImageAttachements.Where(img => PathExtensions.PathEquals(img.ActualSourceFilePath, panDocHtmlImgTagPath)).FirstOrDefault();

                // Only add a new attachment if this is the first time the image is referenced in the page
                if (imgAttach == null)
                {
                    // Add a new attachment to current page
                    imgAttach = new Attachement(page)
                    {
                        Type = AttachementType.Image,
                        ActualSourceFilePath = Path.GetFullPath(panDocHtmlImgTagPath),
                        OriginalUserFilePath = Path.GetFullPath(panDocHtmlImgTagPath) // Not really a user file path but a PanDoc temp file
                    };

                    page.Attachements.Add(imgAttach);

                    EnsureAttachmentFileIsNotUsed(page, imgAttach);
                }

                var attachRef = GetAttachmentMdReference(imgAttach);
                var refLabel = Path.GetFileNameWithoutExtension(imgAttach.ActualSourceFilePath);

                if (outputHtmlTag)
                    return $"<img src=\"{attachRef}\" alt=\"{refLabel}\" />";
                else
                    return $"![{refLabel}]({attachRef})";
            }

            // Match <IMG> tags and any html cell tags arround
            string pattern = @"(?<cellTagStart><(?:td|th)\b[^>]*>(?:(?!<\/(?:td|th)>)[\s\S])*?)?(?<imgTag><img\b[^>]*>)(?<cellTagEnd>(?:(?!<\/(?:td|th)>)[\s\S])*?<\/(?:td|th)>)?";

            var pageTxtModified = Regex.Replace(mdFileContent, pattern, delegate (Match match)
            {
                string imageTag = match.ToString();

                Group cellTagStart = match.Groups["cellTagStart"];
                Group imgTag = match.Groups["imgTag"];
                Group cellTagEnd = match.Groups["cellTagEnd"];

                var imgNestedInHtmlTable = cellTagStart.Success || cellTagEnd.Success;

                var newImg = processImgTag(imageTag, imgNestedInHtmlTable);

                return $"{cellTagStart.Value}{newImg}{cellTagEnd.Value}";
            });


            // Move attachments file into output resource folder and delete tmp file
            // In case of duplicate files, suffix attachment file name
            foreach (var attach in page.ImageAttachements)
            {
                var attachFilePath = GetAttachmentFilePath(attach);
                Directory.CreateDirectory(Path.GetDirectoryName(attachFilePath));
                File.Copy(attach.ActualSourceFilePath, attachFilePath);
                File.Delete(attach.ActualSourceFilePath);
            }


            if (AppSettings.PostProcessingMdImgRef)
            {
                mdFileContent = pageTxtModified;
            }
        }

        /// <summary>
        /// Converts a Windows metafile (EMF or WMF) to PNG format so that markdown viewers can display it.
        /// Screen clippings pasted into OneNote are exported as EMF images by pandoc, which most markdown
        /// viewers cannot display. This method converts them to PNG in place.
        /// </summary>
        /// <param name="imagePath">Path to the image file. Returned unchanged if not an EMF/WMF file.</param>
        /// <returns>Path to the PNG file, or the original path if no conversion was performed.</returns>
        private static string ConvertMetafileToImageIfNeeded(string imagePath)
        {
            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            if (ext != ".emf" && ext != ".wmf")
                return imagePath;

            var pngPath = Path.ChangeExtension(imagePath, ".png");

            // If PNG already exists (e.g. same image referenced twice), reuse it
            if (File.Exists(pngPath))
                return pngPath;

            if (!File.Exists(imagePath))
                return imagePath;

            try
            {
                using (var img = System.Drawing.Image.FromFile(imagePath))
                {
                    var width = img.Width > 0 ? img.Width : 800;
                    var height = img.Height > 0 ? img.Height : 600;

                    using var bmp = new System.Drawing.Bitmap(width, height);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(img, 0, 0, width, height);
                    bmp.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                // Only delete the original after verifying the PNG was created successfully
                if (File.Exists(pngPath))
                    File.Delete(imagePath);

                return pngPath;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to convert metafile {Path.GetFileName(imagePath)} to PNG: {ex.Message}");
                return imagePath;
            }
        }

        /// <summary>
        /// Suffix the attachment file name if it conflicts with an other attachment previously attached to the notebook export
        /// </summary>
        /// <param name="page">The parent Page</param>
        /// <param name="attach">The attachment</param>
        private void EnsureAttachmentFileIsNotUsed(Page page, Attachement attach)
        {
            var notUseFileNameFound = false;
            var cmpt = 0;
            var attachmentFilePath = GetAttachmentFilePath(attach);

            while (!notUseFileNameFound)
            {
                var candidateFilePath = cmpt == 0 ? attachmentFilePath :
                    $"{Path.ChangeExtension(attachmentFilePath, null)}-{cmpt}{Path.GetExtension(attachmentFilePath)}";

                var attachmentFileNameAlreadyUsed = page.GetNotebook().GetAllAttachments().Any(a => a != attach && PathExtensions.PathEquals(GetAttachmentFilePath(a), candidateFilePath));

                // because of using guid, this step should no longer needed and need to be removed
                if (!attachmentFileNameAlreadyUsed)
                {
                    if (cmpt > 0)
                        attach.OverrideExportFilePath = candidateFilePath;

                    notUseFileNameFound = true;
                }
                else
                    cmpt++;
            }

        }


        /// <summary>
        /// Suffix the page file name if it conflicts with an other page previously attached to the notebook export
        /// </summary>
        /// <param name="page">The parent Page</param>
        /// <param name="attach">The attachment</param>
        private void EnsurePageUniquenessPerSection(Page page)
        {
            var notUseFileNameFound = false;
            var cmpt = 0;
            var pageFilePath = GetPageMdFilePath(page);

            while (!notUseFileNameFound)
            {
                var candidateFilePath = cmpt == 0 ? pageFilePath :
                    $"{Path.ChangeExtension(pageFilePath, null)}-{cmpt}.md";

                var attachmentFileNameAlreadyUsed = page.Parent.Childs.OfType<Page>().Any(p => p != page && PathExtensions.PathEquals(GetPageMdFilePath(p), candidateFilePath));

                if (!attachmentFileNameAlreadyUsed)
                {
                    if (cmpt > 0)
                        page.OverridePageFilePath = candidateFilePath;

                    notUseFileNameFound = true;
                }
                else
                    cmpt++;
            }
        }

        private static void ProcessPageAttachments(XNamespace ns, Page page, XElement xmlPageContent)
        {
            foreach (var xmlAttachment in xmlPageContent.Descendants(ns + "InsertedFile").Concat(xmlPageContent.Descendants(ns + "MediaFile")))
            {
                var fileAttachment = new Attachement(page)
                {
                    ActualSourceFilePath = xmlAttachment.Attribute("pathCache")?.Value,
                    OriginalUserFilePath = xmlAttachment.Attribute("pathSource")?.Value,
                    OneNotePreferredFileName = xmlAttachment.Attribute("preferredName")?.Value,
                    Type = AttachementType.File
                };

                if (fileAttachment.ActualSourceFilePath != null)
                {
                    page.Attachements.Add(fileAttachment);
                }
            }
        }
    }
}
