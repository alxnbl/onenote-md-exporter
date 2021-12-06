﻿using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public class JoplinExportService : ExportServiceBase
    {
        private string GetNoteBookFolderRoot(Node node)
            => Path.Combine(node.GetNotebook().ExportFolder, node.GetNotebook().GetNotebookPath());
        protected override string GetResourceFolderPath(Node node)
            => Path.Combine(GetNoteBookFolderRoot(node), "resources");

        protected override string GetPageMdFilePath(Page page)
            => Path.Combine(GetNoteBookFolderRoot(page), page.Id + ".md");


        /// <summary>
        /// Location in the export folder of the original attachment file
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId">Id of the attachment</param>
        /// <param name="oneNoteFilePath">Original filepath of the file in OneNote</param>
        /// <returns></returns>
        protected override string GetAttachmentFilePath(Attachement attachement)
            => Path.Combine(GetResourceFolderPath(attachement.ParentPage), attachement.Id + Path.GetExtension(attachement.FriendlyFileName));

        protected override string GetAttachmentFilePathOnPage(Attachement attachement)
            => Path.Combine(GetResourceFolderPath(attachement.ParentPage), attachement.Id + Path.GetExtension(attachement.FriendlyFileName));


        protected override string GetAttachmentMdReference(Attachement attachement)
            => $":/{attachement.Id}";

        /// <summary>
        /// Path of the joplin md file created in the export folder
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId"></param>
        /// <returns></returns>
        private string GetJoplinAttachmentMdFilePath(Page page, string attachId)

            => Path.Combine(GetNoteBookFolderRoot(page), attachId + ".md");

        private string GetSectionMdFilePath(Node section)
            => Path.Combine(GetNoteBookFolderRoot(section), $"{section.Id}.md");

        public JoplinExportService(AppSettings appSettings, Application oneNoteApp, ConverterService converterService) : base(appSettings, oneNoteApp, converterService)
        {
            _exportFormatCode = "joplin-raw-dir";
        }

        /// <summary>
        /// Export a notebook in Joplin folder format
        /// </summary>
        /// <param name="notebook">The notebook</param>
        /// <param name="sectionNameFilter">Only export the specified section</param>
        /// <param name="pageNameFilter">Only export the specified page</param>
        public override void ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections(true).Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information($"--> Found {sections.Count} sections and sections groups\n");

            // Create the joplin root mdfile of the notebook
            WriteSectionNodeMdFile(notebook);

            // Export each section group and section
            int cmpt = 0;
            foreach (Section section in sections)
            {
                cmpt++;
                Log.Information($"Start processing section ({cmpt}/{sections.Count()}) :  {section.GetPath()}\\{section.Title}");

                WriteSectionNodeMdFile(section);

                if (!section.IsSectionGroup)
                    ExportSectionPages(section, pageNameFilter);
            }
        }

        /// <summary>
        /// Create the joplin md file of the notebook, section group or section
        /// </summary>
        /// <param name="section"></param>
        /// <param name="pageNameFilter"></param>
        private void WriteSectionNodeMdFile(Node section)
        {
            // Write Joplin Section Md File (metadata header + empty content)
            var sectionMd = string.Empty;
            InsertJoplinNodeMetadataFooter(section, ref sectionMd);

            File.WriteAllText(GetSectionMdFilePath(section), sectionMd);
        }

        /// <summary>
        /// Export a joplin md file for each page of the section and process page's attachments
        /// </summary>
        /// <param name="section"></param>
        private void ExportSectionPages(Section section, string pageNameFilter = "")
        {
            Log.Debug($"Start exporting pages of section {section.Title}");

            // Get pages of the section and apply provided filter if any
            var pages = _oneNoteApp.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();

            int cmpt = 0;

            foreach (Page page in pages)
            {
                Log.Information($"   Page {++cmpt}/{pages.Count} : {page.TitleWithPageLevelTabulation}");

                ExportPage(page);
            }
        }


        private string GetJoplinAttachmentMetadata(Attachement attach)
        {

            var exportFilePath = GetAttachmentFilePath(attach);
            var exportFileName = Path.GetFileName(exportFilePath);

            var sb = new StringBuilder();
            sb.Append($"{exportFileName}\n");

            var data = new Dictionary<string, string>();

            data["id"] = attach.Id;
            data["mime"] = MimeTypes.GetMimeType(exportFilePath);
            data["filename"] = attach.FriendlyFileName;
            data["updated_time"] = attach.ParentPage.LastModificationDate.ToString("s");
            data["user_updated_time"] = attach.ParentPage.LastModificationDate.ToString("s");
            data["created_time"] = attach.ParentPage.CreationDate.ToString("s");
            data["user_created_time"] = attach.ParentPage.CreationDate.ToString("s");
            data["file_extension"] = Path.GetExtension(exportFilePath).Replace(".", "");
            data["encryption_cipher_text"] = "";
            data["encryption_applied"] = "0";
            data["encryption_blob_encrypted"] = "0";
            data["size"] = new FileInfo(exportFilePath).Length.ToString();
            data["is_shared"] = "0";
            data["type_"] = "4";

            foreach (var metadata in data)
            {
                sb.Append($"\n{metadata.Key}: {metadata.Value}");
            }

            return sb.ToString();
        }

        private void InsertJoplinNodeMetadataFooter(Node node, ref string mdFileContent)
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
                data["source_application"] = "OneNoteMdExporter";
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

            mdFileContent = sb.ToString();
        }

        /// <summary>
        /// Write the content of the page markdown in the joplin md file
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageMd"></param>
        protected override void WritePageMdFile(Page page, string pageMd)
        {
            // Append the Joplin metadata footer to page md
            InsertJoplinNodeMetadataFooter(page, ref pageMd);

            // Write joplin md file of the page
            File.WriteAllText(GetPageMdFilePath(page), pageMd);
        }

        protected override void FinalizeExportPageAttachemnts(Page page, Attachement attachment)
        {
            // Create joplin md file for the attachment
            File.WriteAllText(GetJoplinAttachmentMdFilePath(page, attachment.Id), GetJoplinAttachmentMetadata(attachment));
        }

        protected override void PreparePageExport(Page page)
        {
            return; // nothing to prepare
        }

    }
}
