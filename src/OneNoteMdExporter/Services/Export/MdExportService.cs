using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
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
        protected override string GetResourceFolderPath(Node node)
            => Path.Combine(node.GetNotebook().ExportFolder, "_resources");

        protected override string GetPageMdFilePath(Page page)
        {
            if(page.OverridePageFilePath == null)
            {
                var hierarchyPrefix = _appSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HiearchyAsFolderTree
                    ? GetPageHierarchyFolderPrefix(page) : "";

                if (hierarchyPrefix == "")
                    return Path.Combine(page.GetNotebook().ExportFolder, page.GetPageFileRelativePath(_appSettings.MdMaxFileLength) + ".md");
                else 
                    return Path.Combine(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), page.TitleWithNoInvalidChars(_appSettings.MdMaxFileLength) + ".md");
            }
            else
            {
                return page.OverridePageFilePath;
            }
        }

        private string GetPageHierarchyFolderPrefix(Page page)
        {
            if (page.ParentPage?.ParentPage != null) // L3
                return Path.Combine(GetPageMdFilePath(page.ParentPage.ParentPage), GetPageMdFilePath(page.ParentPage));
            else if (page.ParentPage != null)
                return GetPageMdFilePath(page.ParentPage);
            else
                return "";
        }

        protected override string GetAttachmentFilePath(Attachement attachement)
        {
            if (attachement.OverrideExportFilePath == null)
                return Path.Combine(GetResourceFolderPath(attachement.ParentPage), attachement.FriendlyFileName.RemoveMdReferenceInvalidChars());         
            else
                return attachement.OverrideExportFilePath;
        }

        /// <summary>
        /// Get relative path from Image's folder to attachement folder
        /// </summary>
        /// <param name="attachement"></param>
        /// <returns></returns>
        protected override string GetAttachmentMdReference(Attachement attachement)
            => Path.GetRelativePath(Path.GetDirectoryName(GetPageMdFilePath(attachement.ParentPage)), GetAttachmentFilePath(attachement)).Replace("\\", "/");

        public MdExportService(AppSettings appSettings, Application oneNoteApp, ConverterService converterService) : base(appSettings, oneNoteApp, converterService)
        {
            _exportFormatCode = "md";
        }

        public override void ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections().Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information($"--> Found {sections.Count} sections\n");

            // Export each section
            int cmptSect = 0;
            foreach (Section section in sections)
            {
                Log.Information($"Start processing section ({++cmptSect}/{sections.Count()}) :  {section.GetPath()}\\{section.Title}");

                if (section.IsSectionGroup)
                    throw new InvalidOperationException("Cannot call ExportSection on section group with MdExport");

                // Get pages list
                var pages = _oneNoteApp.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();

                int cmptPage = 0;

                foreach (Page page in pages)
                {
                    Log.Information($"   Page {++cmptPage}/{pages.Count} : {page.TitleWithPageLevelTabulation}");
                    ExportPage(page);
                }
            }
        }

        protected override void WritePageMdFile(Page page, string pageMd)
        {
            File.WriteAllText(GetPageMdFilePath(page), pageMd);
        }

        protected override void FinalizeExportPageAttachemnts(Page page, Attachement attachment)
        {
            return; // No markdown file generated for attachments
        }

        protected override void PreparePageExport(Page page)
        {
            var pageDirectory = Path.GetDirectoryName(GetPageMdFilePath(page));

            if (!Directory.Exists(pageDirectory))
                Directory.CreateDirectory(pageDirectory);
        }

        protected override string FinalizePageMdPostProcessing(Page page, string md)
        {
            var res = md;

            if (_appSettings.AddFrontMatterHeader)
                res = AddFrontMatterHeader(page, md);

            return res;
        }

        private string AddFrontMatterHeader(Page page, string pageMd)
        {
            var headerModel = new FrontMatterHeader
            {
                Title = page.Title,
                Created = page.CreationDate,
                Updated = page.LastModificationDate
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
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
