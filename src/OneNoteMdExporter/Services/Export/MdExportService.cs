using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Markdown exporter Service
    /// </summary>
    public class MdExportService : ExportServiceBase
    {
        protected override string ExportFormatCode { get; } = "md";

        protected override string GetResourceFolderPath(Page page)
        {
            if (AppSettings.ResourceFolderLocation == ResourceFolderLocationEnum.RootFolder)
                return Path.Combine(page.GetNotebook().ExportFolder, AppSettings.ResourceFolderName);
            else
                return Path.Combine(Path.GetDirectoryName(GetPageMdFilePath(page)), AppSettings.ResourceFolderName);
        }

        protected override string GetPageMdFilePath(Page page)
        {
            if (page.OverridePageFilePath == null)
            {
                var defaultPath = Path.Combine(page.GetNotebook().ExportFolder, page.GetPageFileRelativePath(AppSettings.MdMaxFileLength) + ".md");

                if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsFolderTree)
                {
                    if (page.ParentPage != null)
                        return Path.Combine(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsPageTitlePrefix)
                {
                    if (page.ParentPage != null)
                        return String.Concat(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), AppSettings.PageHierarchyFileNamePrefixSeparator, page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else
                    return defaultPath;
            }
            else
            {
                return page.OverridePageFilePath;
            }
        }

        protected override string GetAttachmentFilePath(Attachement attachment)
        {
            if (attachment.OverrideExportFilePath == null)
                return Path.Combine(GetResourceFolderPath(attachment.ParentPage), attachment.Id + Path.GetExtension(attachment.FriendlyFileName));
            else
                return attachment.OverrideExportFilePath;
        }

        /// <summary>
        /// Get relative path from Image's folder to attachment folder
        /// </summary>
        /// <param name="attachment"></param>
        /// <returns></returns>
        protected override string GetAttachmentMdReference(Attachement attachment)
            => Path.GetRelativePath(Path.GetDirectoryName(GetPageMdFilePath(attachment.ParentPage)), GetAttachmentFilePath(attachment)).Replace("\\", "/");

        public override NotebookExportResult ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            var result = new NotebookExportResult();

            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections().Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information(String.Format(Localizer.GetString("FoundXSections"), sections.Count));

            // Export each section
            int cmptSect = 0;
            foreach (Section section in sections)
            {
                Log.Information($"{Localizer.GetString("StartProcessingSectionX")} ({++cmptSect}/{sections.Count}) :  {section.GetPath(AppSettings.MdMaxFileLength)}\\{section.Title}");

                if (section.IsSectionGroup)
                    throw new InvalidOperationException("Cannot call ExportSection on section group with MdExport");

                // Get pages list
                var pages = OneNoteApp.Instance.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();

                int cmptPage = 0;

                foreach (Page page in pages)
                {
                    Log.Information($"   {Localizer.GetString("Page")} {++cmptPage}/{pages.Count} : {page.TitleWithPageLevelTabulation}");
                    var success = ExportPage(page);

                    if (!success) result.PagesOnError++;
                }
            }

            return result;
        }

        protected override void WritePageMdFile(Page page, string pageMd)
        {
            File.WriteAllText(GetPageMdFilePath(page), pageMd);
        }

        protected override void FinalizeExportPageAttachments(Page page, Attachement attachment)
        {
            return; // No markdown file generated for attachments
        }

        protected override void PrepareFolders(Page page)
        {
            var pageDirectory = Path.GetDirectoryName(GetPageMdFilePath(page));

            if (!Directory.Exists(pageDirectory))
                Directory.CreateDirectory(pageDirectory);
        }

        protected override string FinalizePageMdPostProcessing(Page page, string md)
        {
            var res = md;

            if (AppSettings.AddFrontMatterHeader)
                res = AddFrontMatterHeader(page, md);

            return res;
        }

        private static string AddFrontMatterHeader(Page page, string pageMd)
        {
            var headerModel = new FrontMatterHeader
            {
                Title = page.Title,
                Created = page.CreationDate,
                Updated = page.LastModificationDate
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTypeConverter(new DateTimeConverter(formats: AppSettings.FrontMatterDateFormat, kind: DateTimeKind.Local))
                .Build();
            var headerYaml = serializer.Serialize(headerModel);

            return "---\n" + headerYaml + "---\n\n" + pageMd;
        }

        private class FrontMatterHeader
        {
            public string Title { get; set; }
            public DateTime Updated { get; set; }
            public DateTime Created { get; set; }
        }
    }

}
