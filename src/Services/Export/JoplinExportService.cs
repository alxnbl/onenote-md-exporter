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

            if (Directory.Exists(notebook.Title))
                Directory.Delete(notebook.Title, true);
            Directory.CreateDirectory(notebook.Title);

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);
            Directory.CreateDirectory("tmp");

            _oneNoteApp.FillNodebookTree(notebook);

            var sections = notebook.GetSections(true);

            Log.Information($"--> Found {sections.Count} sections and sections groups\n");

            // Create mdfile of notebook
            ExportSection(notebook);

            int cmpt = 0;
            foreach (var section in sections)
            {
                cmpt++;
                Log.Information($"Start processing section ({cmpt}/{sections.Count()}) :  {section.GetPath()}/{section.Title}");

                ExportSection(section);
            }
        }

        private void ExportSection(Node section)
        {
            var sectionMdFileContent = AddJoplinMetadata(section, "");
            var notebookFolder = section.GetNotebookName();

            File.WriteAllText(Path.Combine(notebookFolder, $"{section.Id}.md"), sectionMdFileContent);

            if (section is Section sectionNode && !sectionNode.IsSectionGroup)
            {
                var pages = _oneNoteApp.GetPages(sectionNode);
                var resourcePath = Path.Combine(notebookFolder, "resources");
                Directory.CreateDirectory(resourcePath);

                foreach (Page page in pages)
                {
                    Log.Information($"{page.TitleWithPageLevelTabulation}");

                    var docxFilePath = Path.Combine("tmp", page.TitleWithNoInvalidChars + ".docx");

                    try
                    {
                        File.Delete(docxFilePath);
                        _oneNoteApp.Publish(page.OneNoteId, Path.GetFullPath(docxFilePath), PublishFormat.pfWord);

                        var mdFilePath = Path.Combine(notebookFolder, $"{page.Id}.md");
                        var mdFileContent = _convertServer.ConvertDocxToMd(page, docxFilePath, resourcePath, section.GetLevel());
                        mdFileContent = _convertServer.PostConvertion(page, mdFileContent, resourcePath, mdFilePath, true);
                        mdFileContent = AddJoplinMetadata(page, mdFileContent);

                        // Create image md file
                        File.WriteAllText(mdFilePath, mdFileContent);

                        foreach (var image in page.Attachements)
                        {
                            // Create attachment md file
                            var mdExtensionFileContent = AddJoplinAttachmentMetadata(image);
                            File.WriteAllText(Path.Combine(notebookFolder, $"{image.Id}.md"), mdExtensionFileContent);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(Localizer.GetString("ErrorDurringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, e.Message);
                    }
                }
            }
        }

        private string AddJoplinAttachmentMetadata(Attachements attach)
        {
            var sb = new StringBuilder();
            sb.Append($"{attach.FileName}\n");

            var data = new Dictionary<string, string>();

            data["id"] = attach.Id;
            data["mime"] = MimeTypes.GetMimeType(attach.ExportFilePath);
            data["filename"] = "";
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

        private string AddJoplinMetadata(Node node, string mdFileContent)
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
