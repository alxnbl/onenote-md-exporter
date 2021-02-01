using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public class JoplinExportService : IExportService
    {
        private readonly AppSettings _appSettings;
        private readonly Application _oneNoteApp;
        private readonly ConverterService _convertServer;

        public JoplinExportService(AppSettings appSettings, Application oneNoteApp, ConverterService converterService)
        {
            _appSettings = appSettings;
            _oneNoteApp = oneNoteApp;
            _convertServer = converterService;
        }

        public void ExportNotebook(Notebook notebook)
        {

            if (Directory.Exists(notebook.GetPath()))
                Directory.Delete(notebook.GetPath(), true);
            Directory.CreateDirectory(notebook.Title);

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);
            Directory.CreateDirectory("tmp");

            try
            {
                _oneNoteApp.FillNodebookTree(notebook);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Localizer.GetString("ErrorDuringNotebookProcessingNbTree"), notebook.Title, notebook.Id, ex.Message);
                return;
            }

            var sections = notebook.GetSections(true);

            Log.Information($"--> Found {sections.Count} sections and sections groups\n");

            // Create mdfile of notebook
            ExportSection(notebook);

            int cmpt = 0;
            foreach (var section in sections)
            {
                cmpt++;
                Log.Information($"Start processing section ({cmpt}/{sections.Count()}) :  {section.GetPath()}\\{section.Title}");

                ExportSection(section);
            }
        }

        private void ExportSection(Node section)
        {
            var sectionMdFileContent = AddJoplinNodeMetadata(section, "");
            var notebookFolder = section.GetNotebookPath();

            string onExportFolder;
            
            if(_appSettings.UserTempFolder)
            {
                onExportFolder = Path.GetTempPath();
            }
            else 
            {
                onExportFolder = Path.Combine("tmp", notebookFolder);
                Directory.CreateDirectory(onExportFolder);
            }

            // Write Section Md File
            File.WriteAllText(Path.Combine(notebookFolder, $"{section.Id}.md"), sectionMdFileContent);

            if (section is Section sectionNode && !sectionNode.IsSectionGroup)
            {
                // For leaf section, export pages
                Log.Debug($"Start export pages of section {section.Title}");

                var pages = _oneNoteApp.GetPages(sectionNode);
                var resourceFolderPath = Path.Combine(notebookFolder, "resources");
                Directory.CreateDirectory(resourceFolderPath);

                int cmpt = 0;

                foreach (Page page in pages)
                {
                    Log.Information($"   Page {++cmpt}/{pages.Count} : {page.TitleWithPageLevelTabulation}");

                    var docxFilePath = Path.Combine(onExportFolder, page.Id + ".docx");

                    try
                    {
                        File.Delete(docxFilePath);

                        Log.Debug($"{page.OneNoteId}: start OneNote docx publish");
                        _oneNoteApp.Publish(page.OneNoteId, Path.GetFullPath(docxFilePath), PublishFormat.pfWord);
                        Log.Debug($"{page.OneNoteId}: success");

                        var mdFilePath = Path.Combine(notebookFolder, $"{page.Id}.md");

                        // Convert docx file into Md using PanDoc
                        var pageMdFileContent = _convertServer.ConvertDocxToMd(page, docxFilePath, resourceFolderPath, section.GetLevel());

                        try
                        {
                            // Copy images extracted from DocX to Export folder and add them in list of attachments of the note
                            pageMdFileContent = _convertServer.ExtractImagesToResourceFolder(page, pageMdFileContent, resourceFolderPath, mdFilePath, true, _appSettings.PostProcessingMdImgRef);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"Page '{page.GetPageFileRelativePath()}': {Localizer.GetString("ErrorImageExtract")}");
                            Log.Debug(ex, ex.Message);
                        }

                        // Export all page attachments
                        pageMdFileContent = ExportPageAttachments(page, pageMdFileContent, notebookFolder, resourceFolderPath);

                        // Apply post processing to Page Md content
                        pageMdFileContent = _convertServer.PostConvertion(page, pageMdFileContent, resourceFolderPath, mdFilePath, true);

                        pageMdFileContent = AddJoplinNodeMetadata(page, pageMdFileContent);

                        // Create page md file
                        File.WriteAllText(mdFilePath, pageMdFileContent);


                        if (!_appSettings.Debug)
                        {
                            File.Delete(docxFilePath);
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Export each page attachment and insert reference to them in Md page
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageMdFileContent"></param>
        /// <param name="notebookFolder"></param>
        /// <param name="resourcePath"></param>
        /// <returns></returns>
        private string ExportPageAttachments(Page page, string pageMdFileContent, string notebookFolder, string resourcePath)
        {
            var pageMdFileContentModified = pageMdFileContent;
            foreach (var attach in page.Attachements)
            {
                if(attach.Type == AttachementType.File)
                {
                    // TODO : replace .bin by real orignal file extension from "original onenote file"
                    var ext = Path.GetExtension(attach.OneNoteFileSourceFilePath);

                    attach.ExportFilePath = Path.Combine(resourcePath, $"{attach.Id}{ext}");

                    // Copy attachment file into export folder
                    File.Copy(attach.OneNoteFilePath, attach.ExportFilePath);
                    File.SetAttributes(attach.ExportFilePath, FileAttributes.Normal); // Prevent exception during deletation of export directory


                    pageMdFileContentModified = InsertMdFileAttachReferences(pageMdFileContentModified, attach);
                }

                // Create attachment md file
                var mdExtensionFileContent = AddJoplinAttachmentMetadata(attach);
                File.WriteAllText(Path.Combine(notebookFolder, $"{attach.Id}.md"), mdExtensionFileContent);
            }

            return pageMdFileContentModified;
        }

        private static string InsertMdFileAttachReferences(string pageMdFileContent, Attachements attach)
        {
            var pageMdFileContentModified = Regex.Replace(pageMdFileContent, "(&lt;){2}(?<fileName>.*)(&gt;){2}", delegate (Match match)
            {
                string refFileName = match.Groups["fileName"]?.Value ?? "";
                string attachOriginalFileName = Path.GetFileName(attach.OneNoteFileSourceFilePath);

                if (refFileName.Equals(attachOriginalFileName))
                {
                    // reference found is corresponding to the attachment being processed
                    return $"[{attachOriginalFileName}](:/{attach.Id})";
                }
                else
                {
                    // not the current attachmeent, ignore
                    return match.Value;
                }
            });

            return pageMdFileContentModified;
        }

        private string AddJoplinAttachmentMetadata(Attachements attach)
        {
            var sb = new StringBuilder();
            sb.Append($"{attach.ExportFileName}\n");

            var data = new Dictionary<string, string>();

            data["id"] = attach.Id;
            data["mime"] = MimeTypes.GetMimeType(attach.ExportFilePath);
            data["filename"] = attach.FriendlyFileName;
            data["updated_time"] = attach.Parent.LastModificationDate.ToString("s");
            data["user_updated_time"] = attach.Parent.LastModificationDate.ToString("s");
            data["created_time"] = attach.Parent.CreationDate.ToString("s");
            data["user_created_time"] = attach.Parent.CreationDate.ToString("s");
            data["file_extension"] = Path.GetExtension(attach.ExportFilePath).Replace(".", "");
            data["encryption_cipher_text"] = "";
            data["encryption_applied"] = "0";
            data["encryption_blob_encrypted"] = "0";
            data["size"] = new FileInfo(attach.ExportFilePath).Length.ToString();
            data["is_shared"] = "0";
            data["type_"] = "4";

            foreach (var metadata in data)
            {
                sb.Append($"\n{metadata.Key}: {metadata.Value}");
            }

            return sb.ToString();
        }

        private string AddJoplinNodeMetadata(Node node, string mdFileContent)
        {
            var sb = new StringBuilder();

            if(node is Page page)
            {
                sb.Append($"{page.TitleWithPageLevelTabulation}\n\n");
            }
            else
            {
                sb.Append($"{node.Title}\n\n");
            }

            sb.Append($"{mdFileContent}\n");

            var data = new Dictionary<string, string>();

            data["id"] = node.Id;
            data["parent_id"] = node.Parent?.Id ?? "";
            data["is_shared"] = "0";
            data["encryption_applied"] = "0";
            data["encryption_cipher_text"] = "";

            data["updated_time"] = node.LastModificationDate.ToString("s");
            data["user_updated_time"] = node.LastModificationDate.ToString("s");
            data["created_time"] = node.CreationDate.ToString("s");
            data["user_created_time"] = node.CreationDate.ToString("s");


            if (node is Page page2)
            {
                data["is_conflict"] = "0";
                data["latitude"] = "0.00000000";
                data["longitude"] = "0.00000000";
                data["altitude"] = "0.0000";
                data["author"] = "";
                data["source_url"] = "";
                data["is_todo"] = "0";
                data["todo_due"] = "0";
                data["todo_completed"] = "0";
                data["source"] = "onenote";
                data["source_application"] = "alxnbl.alxnbl.OneNoteExporter";
                data["application_data"] = "";
                data["order"] = (100000000 - page2.PageSectionOrder * 100000).ToString(); // TODO: replace by the same algorithm used in Joplin to affect page order
                data["markup_language"] = "1";
                data["type_"] = "1";
            }
            else
            {
                data["type_"] = "2";
            }

            foreach (var metadata in data)
            {
                sb.Append($"\n{metadata.Key}: {metadata.Value}");
            }

            return sb.ToString();
        }

    }
}
