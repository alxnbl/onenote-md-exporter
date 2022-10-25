using alxnbl.OneNoteMdExporter.Models;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public interface IExportService
    {
        NotebookExportResult ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "");
    }
}