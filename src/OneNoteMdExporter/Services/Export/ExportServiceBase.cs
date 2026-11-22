using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using alxnbl.OneNoteMdExporter.Services;
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

        /// <summary>
        /// Journal of completed pages for the notebook being exported, used by --resume. Null unless a fixed
        /// output folder (ExportRootFolder) is in use for a Markdown export.
        /// </summary>
        protected ExportJournal Journal { get; private set; }

        /// <summary>
        /// Marks a fixed output folder as belonging to one notebook, so two notebooks cannot share and clean it.
        /// </summary>
        public const string NotebookMarkerFileName = ".notebook-name";

        public NotebookExportResult ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            // A fixed output folder and --resume apply to the Markdown export only. A Joplin export keeps the
            // upstream behaviour: a new timestamped folder each run (its note ids are regenerated every run).
            var useFixedFolder = !string.IsNullOrWhiteSpace(AppSettings.ExportRootFolder) && ExportFormatCode == "md";
            var resume = useFixedFolder && AppSettings.ResumeExport;

            if (useFixedFolder)
                notebook.ExportFolder = Path.GetFullPath(AppSettings.ExportRootFolder);
            else
                notebook.ExportFolder = @$"{Localizer.GetString("ExportFolder")}\{ExportFormatCode}\{notebook.GetNotebookPath()}-{DateTime.Now:yyyyMMdd HH-mm}";

            var notebookFolder = GetNotebookFolderPath(notebook);

            // A notebook folder belongs to one notebook. Two notebooks whose names are equal once cut to
            // MdMaxFileLength, exported to the same fixed root, would otherwise share it and clean each other.
            var markerPath = Path.Combine(notebookFolder, NotebookMarkerFileName);
            if (useFixedFolder && File.Exists(markerPath))
            {
                var owner = File.ReadAllText(markerPath).Trim();
                if (owner.Length > 0 && !string.Equals(owner, notebook.Title, StringComparison.Ordinal))
                    return new NotebookExportResult
                    {
                        NoteBookExportErrorCode = "FolderOfOtherNotebook",
                        NoteBookExportErrorMessage = $"'{notebookFolder}' holds the export of notebook '{owner}', not '{notebook.Title}'. Rename one of the notebooks in OneNote or export to another --output folder."
                    };
            }

            var canResume = resume && File.Exists(Path.Combine(notebookFolder, ExportJournal.FileName));
            if (canResume)
            {
                // Keep what was already exported, only clean the temp folder
                DirectoryHelper.ClearFolder(GetTmpFolder(notebook));
            }
            else
            {
                if (resume)
                    Log.Information($"No previous export found in '{notebookFolder}', starting a full export");
                CleanUpFolder(notebook);
            }

            if (useFixedFolder)
            {
                Directory.CreateDirectory(notebookFolder);
                File.WriteAllText(markerPath, notebook.Title ?? "", new System.Text.UTF8Encoding(false));
            }

            Journal = useFixedFolder ? new ExportJournal(notebookFolder, canResume) : null;

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
            // Resume: if this page was fully exported in a previous run and its file is still there, skip it.
            // Checked before the page content is fetched from OneNote, so a resumed page costs nothing.
            if (!retry && Journal != null && Journal.IsCompleted(page, GetPageMdFilePath(page)))
            {
                Log.Information($"Page '{page.GetPageFileRelativePath(AppSettings.MdMaxFileLength)}': already exported, skipped (resume)");
                return true;
            }

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

                // Keep the page's full path under MaxPathLength (no-op unless the setting is > 0)
                ApplyPathLengthGuard(page);

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

                // Fallback: when the Word/PanDoc route produced nothing for this page, render its markdown
                // straight from the OneNote page XML instead of writing an empty page.
                if (string.IsNullOrWhiteSpace(pageMd) && AppSettings.XmlFallbackRenderer && ExportFormatCode == "md")
                {
                    Log.Information($"Page '{page.TitleWithPageLevelTabulation}': the Word/PanDoc route produced no content, rendering from the OneNote page XML");
                    pageMd = RenderPageFromXml(page, xmlPageContentStr);
                }

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

                // Convert EMF/WMF metafiles written for this page to PNG and point the markdown at the PNG
                ConvertLegacyImages(page, ref pageMd);

                // Make a PDF copy of Office file attachments and link it under the attachment
                ConvertOfficeAttachments(page, ref pageMd);

                // Render XPS printout attachments to PNG pages embedded under the attachment
                ConvertXpsAttachments(page, ref pageMd);

                // Copy audio/video attachments to MP3/MP4 and, if configured, append a machine transcript
                ConvertMediaAttachments(page, ref pageMd);

                // Apply post processing to Page Md content
                ConverterService.PageMdPostConversion(ref pageMd);

                // Optional clean-up pass: unescape PanDoc's \< \>, strip span noise, renumber "1. 1. 1." lists
                if (AppSettings.MarkdownCleanup && ExportFormatCode == "md")
                    pageMd = MarkdownCleanup.CleanupMarkdown(pageMd);

                // Convert OneNote:// links to markdown links
                pageMd = oneNoteLinkTranslatorService.ConvertOneNoteLinks(pageMd, GetPageWikilink);

                // Apply post processing specific to an export format
                pageMd = FinalizePageMdPostProcessing(page, pageMd);

                WritePageMdFile(page, pageMd);

                // Resume: record the page as complete only now, after its md file and attachments are on disk
                Journal?.MarkCompleted(page, GetPageMdFilePath(page));

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

        /// <summary>
        /// Fallback body for a page the Word/PanDoc route produced no content for: render Markdown straight
        /// from the OneNote page XML. The XML is re-parsed from the original string so page XML preprocessing
        /// (which mutates the working tree) cannot interfere. Images embedded in the XML (one:Data) are written
        /// to the page's resource folder by the renderer; file attachments are linked to the copies
        /// ExportPageAttachments makes later in this same export (the paths are deterministic), so no OneNote
        /// binary read is needed.
        /// </summary>
        private string RenderPageFromXml(Page page, string xmlPageContentStr)
        {
            var xml = XDocument.Parse(xmlPageContentStr).Root;
            var ns = xml.Name.Namespace;
            var mdDir = Path.GetDirectoryName(GetPageMdFilePath(page));

            // Map each one:InsertedFile / one:MediaFile to the attachment ProcessPageAttachments registered for
            // it. That method walks InsertedFile then MediaFile and keeps only the ones with a pathCache, so
            // build the map the same way, over the same original XML, to keep the positions aligned.
            var fileElements = xml.Descendants(ns + "InsertedFile")
                .Concat(xml.Descendants(ns + "MediaFile"))
                .Where(e => e.Attribute("pathCache")?.Value != null)
                .ToList();
            var fileAttachments = page.FileAttachements;
            var byElement = new Dictionary<XElement, Attachement>();
            for (var i = 0; i < fileElements.Count && i < fileAttachments.Count; i++)
                byElement[fileElements[i]] = fileAttachments[i];

            var warnings = new List<string>();
            var options = new XmlRenderOptions
            {
                ResourceFolder = GetResourceFolderPath(page),
                MdDir = mdDir,
                ImageKeyPrefix = page.Id,
                IncludeTitle = false, // the title is already carried by the front matter and the file name
                AttachmentRef = element =>
                {
                    if (!byElement.TryGetValue(element, out var attachment)) return null;
                    // ExportPageAttachments copies the file to this exact path later in the same export;
                    // the path is deterministic, so link to it now.
                    return Path.GetRelativePath(mdDir, GetAttachmentFilePath(attachment)).Replace('\\', '/');
                },
                DisplayableCopy = imagePath =>
                    AppSettings.ConvertLegacyFormats && LegacyFormatConverter.IsMetafile(imagePath)
                        ? LegacyFormatConverter.ConvertMetafileToPng(imagePath)
                        : null, // an EMF/WMF image embedded in the page XML: show a PNG copy, keep the original
            };

            var md = XmlMarkdownRenderer.Render(xml, ns, options, warnings);
            foreach (var warning in warnings.Distinct())
                Log.Warning($"Page '{page.TitleWithPageLevelTabulation}': {warning}");
            return md;
        }

        /// <summary>
        /// For every attachment of the page whose file on disk is an EMF/WMF metafile, render a PNG next to it
        /// and point the markdown reference at the PNG. The original metafile is kept. No-op unless
        /// ConvertLegacyFormats is on and the export is Markdown.
        /// </summary>
        private void ConvertLegacyImages(Page page, ref string pageMd)
        {
            if (!AppSettings.ConvertLegacyFormats || ExportFormatCode != "md")
                return;

            foreach (var attach in page.Attachements.ToList())
            {
                var filePath = GetAttachmentFilePath(attach);
                if (!File.Exists(filePath) || !LegacyFormatConverter.IsMetafile(filePath))
                    continue;

                var png = LegacyFormatConverter.ConvertMetafileToPng(filePath);
                if (png == null)
                    continue;

                var oldRef = GetAttachmentMdReference(attach);
                pageMd = pageMd.Replace(oldRef, Path.ChangeExtension(oldRef, ".png"));
            }
        }

        /// <summary>
        /// For every Office file attachment, make a PDF copy next to it (via the installed Office) and insert a
        /// link to the PDF right after the attachment's line in the markdown. The original file is kept. No-op
        /// unless ConvertOfficeToPdf is on and the export is Markdown.
        /// </summary>
        private void ConvertOfficeAttachments(Page page, ref string pageMd)
        {
            if (!AppSettings.ConvertOfficeToPdf || ExportFormatCode != "md")
                return;

            var mdDir = Path.GetDirectoryName(Path.GetFullPath(GetPageMdFilePath(page)));
            var warnings = new List<string>();

            foreach (var attach in page.FileAttachements.ToList())
            {
                var filePath = GetAttachmentFilePath(attach);
                if (!File.Exists(filePath) || !OfficeToPdfConverter.IsOfficeFile(filePath))
                    continue;

                var pdf = OfficeToPdfConverter.OfficeToPdf(filePath, warnings);
                if (pdf == null)
                    continue;

                var pdfRef = Path.GetRelativePath(mdDir, Path.GetFullPath(pdf)).Replace('\\', '/');
                var oldRef = GetAttachmentMdReference(attach);
                pageMd = InsertAfterLineContaining(pageMd, oldRef, $"PDF copy: [{Path.GetFileName(pdf)}]({pdfRef})");
            }

            foreach (var w in warnings.Distinct())
                Log.Warning($"Page '{page.TitleWithPageLevelTabulation}': {w}");
        }

        /// <summary>
        /// For every XPS printout attachment, render one PNG per page next to it and embed the pages right after
        /// the attachment's line in the markdown. The original XPS is kept. No-op unless ConvertLegacyFormats is
        /// on and the export is Markdown.
        /// </summary>
        private void ConvertXpsAttachments(Page page, ref string pageMd)
        {
            if (!AppSettings.ConvertLegacyFormats || ExportFormatCode != "md")
                return;

            var mdDir = Path.GetDirectoryName(Path.GetFullPath(GetPageMdFilePath(page)));

            foreach (var attach in page.FileAttachements.ToList())
            {
                var filePath = GetAttachmentFilePath(attach);
                if (!File.Exists(filePath) || !LegacyFormatConverter.IsXps(filePath))
                    continue;

                var pngs = LegacyFormatConverter.ConvertXpsToPngs(filePath, AppSettings.XpsRenderDpi);
                if (pngs.Count == 0)
                    continue;

                var name = Path.GetFileNameWithoutExtension(filePath);
                var embeds = string.Join(Environment.NewLine + Environment.NewLine,
                    pngs.Select((p, i) => $"![{name} page {i + 1}]({Path.GetRelativePath(mdDir, Path.GetFullPath(p)).Replace('\\', '/')})"));
                pageMd = InsertAfterLineContaining(pageMd, GetAttachmentMdReference(attach), embeds);
            }
        }

        /// <summary>
        /// For audio/video file attachments: copy to MP3/MP4 next to the original (ffmpeg) and link it under the
        /// attachment; and, if a transcription command is configured, append a machine transcript at the end of
        /// the page. No-op unless the export is Markdown and at least one media option is configured.
        /// </summary>
        private void ConvertMediaAttachments(Page page, ref string pageMd)
        {
            if (ExportFormatCode != "md")
                return;

            var mdDir = Path.GetDirectoryName(Path.GetFullPath(GetPageMdFilePath(page)));
            var warnings = new List<string>();
            var transcripts = new List<string>();

            foreach (var attach in page.FileAttachements.ToList())
            {
                var filePath = GetAttachmentFilePath(attach);
                if (!File.Exists(filePath))
                    continue;

                var isAudio = MediaConverter.IsAudioToConvert(filePath);
                var isVideo = MediaConverter.IsVideoToConvert(filePath);
                if (!isAudio && !isVideo)
                    continue;

                var copy = isVideo ? MediaConverter.ToMp4(filePath, warnings) : MediaConverter.ToMp3(filePath, warnings);
                if (copy != null)
                {
                    var copyRef = Path.GetRelativePath(mdDir, Path.GetFullPath(copy)).Replace('\\', '/');
                    pageMd = InsertAfterLineContaining(pageMd, GetAttachmentMdReference(attach), $"{(isVideo ? "MP4" : "MP3")} copy: [{Path.GetFileName(copy)}]({copyRef})");
                }

                var transcript = MediaConverter.Transcribe(filePath, warnings);
                if (!string.IsNullOrWhiteSpace(transcript))
                    transcripts.Add($"## Transcript of {attach.FriendlyFileName}" + Environment.NewLine + Environment.NewLine
                        + "*Machine transcription (may contain errors).*" + Environment.NewLine + Environment.NewLine + transcript);
            }

            if (transcripts.Count > 0)
                pageMd = pageMd.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, transcripts) + Environment.NewLine;

            foreach (var w in warnings.Distinct())
                Log.Warning($"Page '{page.TitleWithPageLevelTabulation}': {w}");
        }

        /// <summary>Insert a line of markdown right after the line that contains <paramref name="marker"/>, or at the end if not found.</summary>
        private static string InsertAfterLineContaining(string md, string marker, string toInsert)
        {
            var idx = md.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
                return md + Environment.NewLine + Environment.NewLine + toInsert + Environment.NewLine;

            var lineEnd = md.IndexOf('\n', idx);
            if (lineEnd < 0) lineEnd = md.Length;
            return md[..lineEnd] + Environment.NewLine + Environment.NewLine + toInsert + Environment.NewLine + md[lineEnd..];
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
        /// <summary>
        /// Keep a page's full path under AppSettings.MaxPathLength. First shortens the file name (with a short
        /// hash to stay unique); if the folder itself is already too long, the page is written to a "_long-paths"
        /// folder at the root of the notebook folder. Room is reserved next to the md file for the resources
        /// folder and attachment file names. No-op unless MaxPathLength > 0 and the export is Markdown.
        /// </summary>
        private void ApplyPathLengthGuard(Page page)
        {
            if (AppSettings.MaxPathLength <= 0 || ExportFormatCode != "md")
                return;

            // Room needed next to the md file: "\<resources>\" + 32-char id + extension (generously "-p99.png")
            var attachmentMargin = AppSettings.ResourceFolderName.Length + 2 + 32 + 10;

            var mdPath = Path.GetFullPath(GetPageMdFilePath(page));
            var limit = AppSettings.MaxPathLength;

            if (mdPath.Length + attachmentMargin <= limit)
                return;

            var dir = Path.GetDirectoryName(mdPath);
            var stem = Path.GetFileNameWithoutExtension(mdPath);
            var hash = ShortHash(page.OneNoteId ?? page.Id);
            var available = limit - attachmentMargin - dir.Length - 1 - ".md".Length - hash.Length - 1;

            string newPath;
            if (available >= 12)
            {
                newPath = Path.Combine(dir, stem[..Math.Min(stem.Length, available)].TrimEnd() + "~" + hash + ".md");
            }
            else
            {
                var overflowDir = Path.Combine(Path.GetFullPath(GetNotebookFolderPath(page.GetNotebook())), "_long-paths");
                var availableOverflow = limit - attachmentMargin - overflowDir.Length - 1 - ".md".Length - hash.Length - 1;
                newPath = Path.Combine(overflowDir, stem[..Math.Clamp(availableOverflow, 0, stem.Length)].TrimEnd() + "~" + hash + ".md");
            }

            Log.Warning($"Page '{page.Title}': path too long ({mdPath.Length} chars + {attachmentMargin} reserved for attachments > MaxPathLength {limit}), exported as '{newPath}'");
            page.OverridePageFilePath = newPath;
        }

        /// <summary>First 8 hex characters of the SHA-1 of the value, lower-case; used to keep a shortened file name unique.</summary>
        private static string ShortHash(string value)
        {
            var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(value ?? ""));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

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
