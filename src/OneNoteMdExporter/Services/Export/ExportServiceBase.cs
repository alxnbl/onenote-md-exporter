using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
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
                
                
                //// Generate "onenote:" link to extract section-id and page-id (eg: "Pag 2")
                //OneNoteApp.Instance.GetHyperlinkToObject("{095C0BE4-7C7A-4CA5-92D3-9835FDD105E5}{1}{E19538458627694935540520157643112716112073471}", null, out var test);


                // Alternative : return page content without binaries
                //oneNoteApp.GetHierarchy(page.OneNoteId, HierarchyScope.hsChildren, out var xmlAttach);

                var xmlPageContent = XDocument.Parse(xmlPageContentStr).Root;
                var ns = xmlPageContent.Name.Namespace;
                page.Author = xmlPageContent.Element(ns + "Title")?.Element(ns + "OE")?.Attribute("author")?.Value ?? "unknown";
                ProcessPageAttachments(ns, page, xmlPageContent);

                // Suffix page title
                EnsurePageUniquenessPerSection(page);

                // Make various OneNote XML fixes before page export
                page.OverrideOneNoteId = PageXmlPreProcessing(xmlPageContent);

                // Register page and section mappings for link conversion
                var pagePath = page.GetPageFileRelativePath(AppSettings.MdMaxFileLength);
                
                // Generate programmatic ID for the page
                string pageProgrammaticId = null;
                try
                {
                    OneNoteApp.Instance.GetHyperlinkToObject(page.OneNoteId, null, out string pageLink);
                    var pageIdMatch = Regex.Match(pageLink, @"page-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                    if (pageIdMatch.Success)
                    {
                        pageProgrammaticId = pageIdMatch.Groups[1].Value;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to generate programmatic ID for page {page.Title}: {ex.Message}");
                }
                
                ConverterService.RegisterPageMapping(page.OneNoteId, pageProgrammaticId, pagePath, page.Title);

                // Generate programmatic ID for the section
                string sectionProgrammaticId = null;
                try
                {
                    if (page.Parent?.OneNoteId != null)
                    {
                        OneNoteApp.Instance.GetHyperlinkToObject(page.Parent.OneNoteId, null, out string sectionLink);
                        var sectionIdMatch = Regex.Match(sectionLink, @"section-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                        if (sectionIdMatch.Success)
                        {
                            sectionProgrammaticId = sectionIdMatch.Groups[1].Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to generate programmatic ID for section {page.Parent?.Title}: {ex.Message}");
                }
                
                if (page.Parent?.OneNoteId != null)
                {
                    ConverterService.RegisterSectionMapping(page.Parent.OneNoteId, sectionProgrammaticId, page.Parent.GetPath(AppSettings.MdMaxFileLength), page.Parent.Title);
                }

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

        /// <summary>
        /// Pre-process OneNote XML page for: Sections unfold, Convert OneNote tags to #hash-tags, Keep checkboxes, etc.
        /// If page XML content was changed due pre-processing, the new content stored at temporary notebook
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

            /// Fix for non-standard text highlights:
            /// Replace OneNote CDATA HTML tags <span style="background:#SOME_HEX_VAL"> by <span style="background:yellow">
            var highlightRegex = new Regex(@"(<span\s+style='[^']*?background)\s*:\s*#\w+");
            foreach (var xmlText in xmlPageContent.Descendants(ns + "T"))
            {
                var isValueChanged = false;

                var newValue = highlightRegex.Replace(xmlText.Value, match =>
                {
                    isValueChanged = true;
                    return $"{match.Groups[1]}:yellow";
                });

                if (isValueChanged)
                    xmlText.Value = newValue;
            }

            if (isXmlChanged)
                return TemporaryNotebook.ClonePage(xmlPageContent);
            else
                return null;
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
            // Replace <IMG> tags by markdown references
            var pageTxtModified = Regex.Replace(mdFileContent, "<img [^>]+/>", delegate (Match match)
            {
                string imageTag = match.ToString();

                // http://regexstorm.net/tester
                string regexImgAttributes = "<img src=\"(?<src>[^\"]+)\".* />";

                MatchCollection matches = Regex.Matches(imageTag, regexImgAttributes, RegexOptions.IgnoreCase);
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
                return $"![{refLabel}]({attachRef})";

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
