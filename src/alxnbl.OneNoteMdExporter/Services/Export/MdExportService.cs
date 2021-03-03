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
    public class MdExportService : IExportService
    {
        private readonly AppSettings _appSettings;
        private readonly Application _oneNoteApp;
        private readonly ConverterService _convertServer;

        public MdExportService(AppSettings appSettings, Application oneNoteApp, ConverterService converterService)
        {
            _appSettings = appSettings;
            _oneNoteApp = oneNoteApp;
            _convertServer = converterService;
        }

        public void ExportNotebook(Notebook notebook)
        {

            if (Directory.Exists(notebook.Title))
                Directory.Delete(notebook.Title, true);

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);
            Directory.CreateDirectory("tmp");

            _oneNoteApp.FillNodebookTree(notebook);

            var sections = notebook.GetSections();

            Log.Information($"--> Found {sections.Count} sections\n");

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
            if (!(section is Section sectionNote) || sectionNote.IsSectionGroup)
                throw new InvalidOperationException("Cannot call ExportSection on section group with MdExport");

            var pages = _oneNoteApp.GetPages(sectionNote);

            var resourceFolderPath = Path.Combine(section.GetNotebookPath(), "_resources");
            Directory.CreateDirectory(resourceFolderPath);

            int cmpt = 0;

            foreach (Page page in pages)
            {
                try
                {
                    Log.Information($"   Page {++cmpt}/{pages.Count} : {page.TitleWithPageLevelTabulation}");

                    Directory.CreateDirectory(page.GetPageFolderRelativePath());

                    var docxFilePath = page.GetPageFileRelativePath() + ".docx";

                    File.Delete(docxFilePath);
                    _oneNoteApp.Publish(page.OneNoteId, Path.GetFullPath(docxFilePath), PublishFormat.pfWord);

                    var mdFileContent = _convertServer.ConvertDocxToMd(page, docxFilePath, resourceFolderPath, section.GetLevel());
                    var mdFilePath = page.GetPageFileRelativePath() + ".md";

                    try
                    {
                        mdFileContent = _convertServer.ExtractImagesToResourceFolder(page, mdFileContent, resourceFolderPath, mdFilePath, true, _appSettings.PostProcessingMdImgRef);
                    }
                    catch (Exception ex)
                    {
                        if (_appSettings.Debug)
                            Log.Warning($"Page '{page.GetPageFileRelativePath()}': {Localizer.GetString("ErrorImageExtract")}");
                        else
                            Log.Warning(ex, $"Page '{page.GetPageFileRelativePath()}'.");
                    }

                    mdFileContent = _convertServer.PostConvertion(page, mdFileContent, resourceFolderPath, mdFilePath, false);

                    File.WriteAllText(mdFilePath, mdFileContent);
                }
                catch (Exception e)
                {
                    Log.Error(Localizer.GetString("ErrorDuringPageProcessing"), page.TitleWithPageLevelTabulation, page.Id, e.Message);
                }
            }
        }
    }
}
