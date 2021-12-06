using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public class MdExportService : ExportServiceBase
    {
        protected override string GetResourceFolderPath(Node node)
            => Path.Combine(node.GetNotebook().ExportFolder, "File");

        protected override string GetPageMdFilePath(Page page)
        {
            if (page.OverridePageFilePath == null)
                return Path.Combine(page.GetNotebook().ExportFolder, page.GetPageFileRelativePath().Left(50) + ".md");
            else
                return page.OverridePageFilePath;
        }

        protected override string GetAttachmentFilePath(Attachement attachement)
        {
            if (attachement.OverrideExportFilePath == null)
                return Path.Combine(GetResourceFolderPath(attachement.ParentPage), attachement.FriendlyFileName.RemoveMdReferenceInvalidChars());         
            else
                return attachement.OverrideExportFilePath;
        }
        protected override string GetAttachmentFilePathOnPage(Attachement attachement) ////tlich: creat a new func to svae Images in page Directory
        {
            if (attachement.OverrideExportFilePath == null)
                return Path.Combine(Path.GetDirectoryName(GetPageMdFilePath(attachement.ParentPage)), "_resources", attachement.FriendlyFileName.RemoveMdReferenceInvalidChars());
            else
            {
                string s1 = attachement.OverrideExportFilePath;
                string s2 = String.Empty;
                //先求出最后出现这个字符的下标
                int index = s1.LastIndexOf('\\');
                //从下一个索引开始截取
                s2 = s1.Substring(index + 1);
                return Path.Combine(Path.GetDirectoryName(GetPageMdFilePath(attachement.ParentPage)), "_resources", s2);
                //return attachement.OverrideExportFilePath;
            }
        }

        /// <summary>
        /// Get relative path from Image's folder to attachement folder
        /// </summary>
        /// <param name="attachement"></param>
        /// <returns></returns>
        protected override string GetAttachmentMdReference(Attachement attachement)
            => Path.GetRelativePath(Path.GetDirectoryName(GetPageMdFilePath(attachement.ParentPage)), GetAttachmentFilePathOnPage(attachement)).Replace("\\", "/"); //GetAttachmentFilePath->GetAttachmentFilePathOnPage

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

            Directory.CreateDirectory(Path.Combine(pageDirectory, "_resources"));//tlich:creat resources every pageDirectory
            if (!Directory.Exists(pageDirectory))
                Directory.CreateDirectory(pageDirectory);
        }

    }
}
