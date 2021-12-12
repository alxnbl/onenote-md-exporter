using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Base class for Export Service. 
    /// Contains all shared logic between exporter of differents formats.
    /// Abstract methods needs to be implemented by each exporter
    /// </summary>
    public abstract class ExportServiceBase : IExportService
    {
        protected readonly AppSettings _appSettings;
        protected readonly Application _oneNoteApp;
        protected readonly ConverterService _convertServer;

        protected string _exportFormatCode;

        public ExportServiceBase(AppSettings appSettings, Application oneNoteApp, ConverterService converterService)
        {
            _appSettings = appSettings;
            _oneNoteApp = oneNoteApp;
            _convertServer = converterService;
        }


        protected string GetNotebookFolderPath(Notebook notebook)
            => Path.Combine(notebook.ExportFolder, notebook.GetNotebookPath());

        /// <summary>
        /// Return location in the export folder of an attachment file
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId">Id of the attachment</param>
        /// <param name="oneNoteFilePath">Original filepath of the file in OneNote</param>
        /// <returns></returns>
        protected abstract string GetAttachmentFilePath(Attachement attachement);

        /// <summary>
        /// Get the md reference to the attachment
        /// </summary>
        /// <param name="attachement"></param>
        /// <returns></returns>
        protected abstract string GetAttachmentMdReference(Attachement attachement);

        protected abstract string GetResourceFolderPath(Node node);

        protected abstract string GetPageMdFilePath(Page page);


        public void ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            notebook.ExportFolder = $"{Localizer.GetString("ExportFolder")}\\{_exportFormatCode}\\{notebook.GetNotebookPath()}-{DateTime.Now.ToString("yyyyMMdd HH-mm")}";
            CleanUpFolder(notebook);
            Directory.CreateDirectory(GetResourceFolderPath(notebook));
            
            // Initialize hierarchy of the notebook from OneNote APIs
            try
            {
                _oneNoteApp.FillNodebookTree(notebook);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Localizer.GetString("ErrorDuringNotebookProcessingNbTree"), notebook.Title, notebook.Id, ex.Message);
                return;
            }

            ExportNotebookInTargetFormat(notebook, sectionNameFilter, pageNameFilter);
        }

        public abstract void ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "");

        private void CleanUpFolder(Notebook notebook)
        {
            // Cleanup Notebook export folder
            DirectoryHelper.ClearFolder(GetNotebookFolderPath(notebook));

            // Cleanup temp folder
            DirectoryHelper.ClearFolder(GetTmpFolder(notebook));
        }

        protected abstract void PreparePageExport(Page page);

        protected static string GetTmpFolder(Node node)
            => Path.Combine(Path.GetTempPath(), node.GetNotebookPath());

        /// <summary>
        /// Export a Page and its attachments
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        protected void ExportPage(Page page)
        {
            // Suffix page title
            EnsurePageUniquenessPerSection(page);

            var docxFileTmpFile = Path.Combine(GetTmpFolder(page), page.Id + ".docx");

            try
            {
                if (File.Exists(docxFileTmpFile))
                    File.Delete(docxFileTmpFile);

                PreparePageExport(page);

                Log.Debug($"{page.OneNoteId}: start OneNote docx publish");

                // Request OneNote to export the page into a DocX file
                _oneNoteApp.Publish(page.OneNoteId, Path.GetFullPath(docxFileTmpFile), PublishFormat.pfWord);

                Log.Debug($"{page.OneNoteId}: success");


                // Convert docx file into Md using PanDoc
                var pageMd = _convertServer.ConvertDocxToMd(page, docxFileTmpFile, GetTmpFolder(page));

                if (_appSettings.Debug)
                {
                    // If debug mode enabled, copy the page docx file next to the page md file
                    var docxFilePath = Path.ChangeExtension(GetPageMdFilePath(page), "docx");
                    File.Copy(docxFileTmpFile, docxFilePath);

                    // And write Pandoc markdown file
                    var mdPanDocFilePath = Path.ChangeExtension(GetPageMdFilePath(page), "pandoc.md");
                    File.WriteAllText(mdPanDocFilePath, pageMd);
                }

                File.Delete(docxFileTmpFile);

                // Copy images extracted from DocX to Export folder and add them in the list of attachments of the page
                try
                {
                    ExtractImagesToResourceFolder(page, ref pageMd, _appSettings.PostProcessingMdImgRef);
                }
                catch (COMException ex)
                {
                    if (ex.Message.Contains("0x800706BE"))
                    {
                        LogError(page, ex, Localizer.GetString("ErrorWhileStartingOnenote"));
                    }
                    else
                        LogError(page, ex, String.Format(Localizer.GetString("ErrorDuringOneNoteExport"), ex.Message));
                }
                catch (Exception ex)
                {
                    LogError(page, ex, Localizer.GetString("ErrorImageExtract"));
                }

                // Export all file attachments and get updated page markdown including md reference to attachments
                ExportPageAttachments(page, ref pageMd);

                // Apply post processing to Page Md content
                _convertServer.PageMdPostConvertion(page, ref pageMd);

                // Apply post processing specific to an export format
                pageMd = FinalizePageMdPostProcessing(page, pageMd);

                WritePageMdFile(page, pageMd);
            }
            catch (Exception ex)
            {
                LogError(page, ex, String.Format(Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, ex.Message));
            }
        }

        protected abstract string FinalizePageMdPostProcessing(Page page, string md);

        private void LogError(Page p, Exception ex, string message)
        {
            Log.Warning($"Page '{p.GetPageFileRelativePath(_appSettings.MdMaxFileLength)}': {message}");
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

                    // Copy attachment file into export folder
                    File.Copy(attach.ActualSourceFilePath, exportFilePath);
                    //File.SetAttributes(exportFilePath, FileAttributes.Normal); // Prevent exception during deletation of export directory

                    // Update page markdown to insert md references to attachments
                    InsertPageMdAttachmentReference(ref pageMdFileContent, attach, GetAttachmentMdReference);
                }

                FinalizeExportPageAttachemnts(page, attach);
            }
        }

        
        /// <summary>
        /// Final class needs to implement logic to write the md file of the attachment file in the export folder (if needed)
        /// </summary>
        /// <param name="page">The page</param>
        /// <param name="attachment">The attachment</param>
        protected abstract void FinalizeExportPageAttachemnts(Page page, Attachement attachment);


        /// <summary>
        /// Replace the tag <<FileName>> generated by OneNote by a markdown link referencing the attachment
        /// </summary>
        /// <param name="pageMdFileContent"></param>
        /// <param name="attach"></param>
        private void InsertPageMdAttachmentReference(ref string pageMdFileContent, Attachement attach, Func<Attachement, string> getAttachMdReferenceMethod)
        {
            var pageMdFileContentModified = Regex.Replace(pageMdFileContent, "(\\\\<){2}(?<fileName>.*)(>\\\\>)", delegate (Match match)
            {
                var refFileName = match.Groups["fileName"]?.Value ?? "";
                var attachOriginalFileName = attach.OneNotePreferredFileName ;
                var attachMdRef = getAttachMdReferenceMethod(attach);

                if (refFileName.Equals(attachOriginalFileName))
                {
                    // reference found is corresponding to the attachment being processed
                    return $"[{attachOriginalFileName}]({attachMdRef})";
                }
                else
                {
                    // not the current attachmeent, ignore
                    return match.Value;
                }
            });

            pageMdFileContent = pageMdFileContentModified;
        }


        /// <summary>
        /// Replace PanDoc IMG HTML tag by markdown reference and copy image file into notebook export directory
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent">Contennt of the MD file</param>
        /// <param name="resourceFolderPath">The path to the notebook folder where store attachments</param>
        /// <param name="postProcessingMdImgRef">If false, markdown reference to image will not be inserted</param>
        /// <param name="getImgMdReferenceMethod">The method that returns the md reference of an image attachment</param>
        public void ExtractImagesToResourceFolder(Page page, ref string mdFileContent, bool postProcessingMdImgRef)
        {
            // Replace <IMG> tags by markdown references
            var pageTxtModified = Regex.Replace(mdFileContent, "<img [^>]+/>", delegate (Match match)
            {

                string imageTag = match.ToString();

                // http://regexstorm.net/tester
                string regexImgAttributes = "<img src=\"(?<src>[^\"]+)\".* />";

                MatchCollection matchs = Regex.Matches(imageTag, regexImgAttributes, RegexOptions.IgnoreCase);
                Match imgMatch = matchs[0];

                var panDocHtmlImgTagPath = Path.GetFullPath(imgMatch.Groups["src"].Value);

                Attachement imgAttach = page.ImageAttachements.Where(img => PathExtensions.PathEquals(img.ActualSourceFilePath, panDocHtmlImgTagPath)).FirstOrDefault();

                // Only add a new attachment if this is the first time the image is referenced in the page
                if (imgAttach == null)
                {
                    // Add a new attachmeent to current page
                    imgAttach = new Attachement(page)
                    {
                        Type = AttachementType.Image,
                    };

                    imgAttach.ActualSourceFilePath = Path.GetFullPath(panDocHtmlImgTagPath);
                    imgAttach.OriginalUserFilePath = Path.GetFullPath(panDocHtmlImgTagPath); // Not really a use file path but a PanDoc temp file

                    page.Attachements.Add(imgAttach);

                    EnsureAttachmentFileIsNotUsed(page, imgAttach);
                }

                var attachRef = GetAttachmentMdReference(imgAttach);
                var refLabel = Path.GetFileNameWithoutExtension(imgAttach.ActualSourceFilePath);

                return $"![{refLabel}]({attachRef})";
            });


            // Move attachements file into output ressource folder and delete tmp file
            // In case of dupplicate files, suffix attachment file name
            foreach (var attach in page.ImageAttachements)
            {
                File.Copy(attach.ActualSourceFilePath, GetAttachmentFilePath(attach));
                File.Delete(attach.ActualSourceFilePath);
            }


            if (postProcessingMdImgRef)
            {
                mdFileContent = pageTxtModified;
            }
        }

        /// <summary>
        /// Suffix the attachment file name if it conflicits with an other attachement previously attached to the notebook export
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
        /// Suffix the page file name if it conflicits with an other page previously attached to the notebook export
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

    }
}
