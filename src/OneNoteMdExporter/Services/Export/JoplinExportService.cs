using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Joplin exporter service
    /// </summary>
    public class JoplinExportService : ExportServiceBase
    {
        protected override string ExportFormatCode { get; } = "joplin-raw-dir";

        private static string GetNoteBookFolderRoot(Node node)
            => Path.Combine(node.GetNotebook().ExportFolder, node.GetNotebook().GetNotebookPath());
        
        protected override string GetResourceFolderPath(Page node)
            => Path.Combine(GetNoteBookFolderRoot(node), AppSettings.ResourceFolderName);

        protected override string GetPageMdFilePath(Page page)
            => Path.Combine(GetNoteBookFolderRoot(page), page.Id + ".md");


        /// <summary>
        /// Location in the export folder of the original attachment file
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId">Id of the attachment</param>
        /// <param name="oneNoteFilePath">Original file path of the file in OneNote</param>
        /// <returns></returns>
        protected override string GetAttachmentFilePath(Attachement attachment)
            => Path.Combine(GetResourceFolderPath(attachment.ParentPage), attachment.Id + Path.GetExtension(attachment.FriendlyFileName));

        protected override string GetAttachmentMdReference(Attachement attachment)
            => $":/{attachment.Id}";

        /// <summary>
        /// Path of the joplin md file created in the export folder
        /// </summary>
        /// <param name="page"></param>
        /// <param name="attachId"></param>
        /// <returns></returns>
        private static string GetJoplinAttachmentMdFilePath(Page page, string attachId)
            => Path.Combine(GetNoteBookFolderRoot(page), attachId + ".md");

        private static string GetSectionMdFilePath(Node section)
            => Path.Combine(GetNoteBookFolderRoot(section), $"{section.Id}.md");

        /// <summary>
        /// Export a notebook in Joplin folder format
        /// </summary>
        /// <param name="notebook">The notebook</param>
        /// <param name="sectionNameFilter">Only export the specified section</param>
        /// <param name="pageNameFilter">Only export the specified page</param>
        public override NotebookExportResult ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            var result = new NotebookExportResult();

            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections(true).Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information(string.Format(Localizer.GetString("FoundXSectionsAndSecGrp"), sections.Count));

            // Phase 1: Build complete tree and collect metadata
            Log.Information("Phase 1: Building notebook tree and collecting metadata...");
            var allNodes = new List<Node>(); // Will contain both sections and pages
            var allPages = new List<Page>();

            // First, collect all sections and section groups
            foreach (Section section in sections)
            {
                allNodes.Add(section);
                
                // Register section mapping with programmatic ID
                try
                {
                    OneNoteApp.Instance.GetHyperlinkToObject(section.OneNoteId, null, out string sectionLink);
                    var sectionIdMatch = Regex.Match(sectionLink, @"section-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                    if (sectionIdMatch.Success)
                    {
                        var programmaticId = sectionIdMatch.Groups[1].Value;
                        ConverterService.RegisterSectionMapping(section.OneNoteId, programmaticId, section.GetPath(AppSettings.MdMaxFileLength), section.Title);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to generate programmatic ID for section {section.Title}: {ex.Message}");
                }

                if (!section.IsSectionGroup)
                {
                    // Get pages list and collect metadata
                    var pages = OneNoteApp.Instance.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();
                    allPages.AddRange(pages);
                    allNodes.AddRange(pages);
                }
            }

            // Phase 2: Export content and convert to markdown
            Log.Information("Phase 2: Exporting content and converting to markdown...");

            // First export all sections (including section groups)
            int cmptSect = 0;
            foreach (Section section in sections)
            {
                Log.Information($"{Localizer.GetString("StartProcessingSectionX")} ({++cmptSect}/{sections.Count}) :  {section.GetPath(AppSettings.MdMaxFileLength)}\\{section.Title}");
                WriteSectionNodeMdFile(section);
            }

            // Then export all pages
            int cmptPage = 0;
            foreach (Page page in allPages)
            {
                if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsFolderTree)
                {
                    MovePageHierarchyInADedicatedNotebook(page);
                }

                Log.Information($"   {Localizer.GetString("Page")} {++cmptPage}/{allPages.Count} : {page.TitleWithPageLevelTabulation}");
                var success = ExportPage(page);

                if (!success) result.PagesOnError++;
            }

            return result;
        }

        /// <summary>
        /// Create the joplin md file of the notebook, section group or section
        /// </summary>
        /// <param name="section"></param>
        /// <param name="pageNameFilter"></param>
        private static void WriteSectionNodeMdFile(Node section)
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
        private SectionExportResult ExportSectionPages(Section section, string pageNameFilter = "")
        {
            var result = new SectionExportResult();

            Log.Debug($"Start exporting pages of section {section.Title}");

            // Get pages of the section and apply provided filter if any
            var pages = OneNoteApp.Instance.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();

            int cmpt = 0;

            foreach (Page page in pages)
            {
                if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsFolderTree)
                {
                    MovePageHierarchyInADedicatedNotebook(page);
                }

                Log.Information($"   {Localizer.GetString("Page")} {++cmpt}/{pages.Count} : {page.TitleWithPageLevelTabulation}");

                var success = ExportPage(page);

                if (!success) result.PagesOnError++;
            }

            return result;
        }

        private static void MovePageHierarchyInADedicatedNotebook(Page page)
        {
            if (page.ChildPages.Any())
            {
                // Page has subpage attached to it => Create a new section and attach the page to it

                var pageSection = new Section(page.Parent)
                {
                    Title = page.Title,
                    CreationDate = page.CreationDate,
                    LastModificationDate = page.LastModificationDate,
                    IsSectionGroup = false
                };

                foreach (var subpage in page.ChildPages)
                {
                    subpage.ReplaceParent(pageSection);
                }

                page.ReplaceParent(pageSection);

                WriteSectionNodeMdFile(pageSection);
            }
        }

        private string GetJoplinAttachmentMetadata(Attachement attach)
        {

            var exportFilePath = GetAttachmentFilePath(attach);
            var exportFileName = Path.GetFileName(exportFilePath);

            var sb = new StringBuilder();
            sb.Append($"{exportFileName}\n");

            var data = new Dictionary<string, string>
            {
                ["id"] = attach.Id,
                ["mime"] = MimeTypes.GetMimeType(exportFilePath),
                ["filename"] = attach.FriendlyFileName,
                ["updated_time"] = attach.ParentPage.LastModificationDate.ToString("s"),
                ["user_updated_time"] = attach.ParentPage.LastModificationDate.ToString("s"),
                ["created_time"] = attach.ParentPage.CreationDate.ToString("s"),
                ["user_created_time"] = attach.ParentPage.CreationDate.ToString("s"),
                ["file_extension"] = Path.GetExtension(exportFilePath).Replace(".", ""),
                ["encryption_cipher_text"] = "",
                ["encryption_applied"] = "0",
                ["encryption_blob_encrypted"] = "0",
                ["size"] = new FileInfo(exportFilePath).Length.ToString(),
                ["is_shared"] = "0",
                ["type_"] = "4"
            };

            foreach (var metadata in data)
            {
                sb.Append($"\n{metadata.Key}: {metadata.Value}");
            }

            return sb.ToString();
        }

        private static void InsertJoplinNodeMetadataFooter(Node node, ref string mdFileContent)
        {
            var sb = new StringBuilder();

            if (node is Page page && AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsPageTitlePrefix)
            {
                sb.Append($"{page.TitleWithPageLevelTabulation}{Environment.NewLine}{Environment.NewLine}");
            }
            else
            {
                sb.Append($"{node.Title}{Environment.NewLine}{Environment.NewLine}");
            }

            sb.Append($"{mdFileContent}{Environment.NewLine}");

            var data = new Dictionary<string, string>
            {
                ["id"] = node.Id,
                ["parent_id"] = node.Parent?.Id ?? "",
                ["is_shared"] = "0",
                ["encryption_applied"] = "0",
                ["encryption_cipher_text"] = "",

                ["updated_time"] = node.LastModificationDate.ToString("s"),
                ["user_updated_time"] = node.LastModificationDate.ToString("s"),
                ["created_time"] = node.CreationDate.ToString("s"),
                ["user_created_time"] = node.CreationDate.ToString("s")
            };


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
                sb.Append($"{Environment.NewLine}{metadata.Key}: {metadata.Value}");
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

        protected override void FinalizeExportPageAttachments(Page page, Attachement attachment)
        {
            // Create joplin md file for the attachment
            File.WriteAllText(GetJoplinAttachmentMdFilePath(page, attachment.Id), GetJoplinAttachmentMetadata(attachment));
        }

        protected override void PrepareFolders(Page page)
        {
            return; // nothing to prepare
        }

        protected override string FinalizePageMdPostProcessing(Page page, string md)
        {
            return md;
        }
    }
}
