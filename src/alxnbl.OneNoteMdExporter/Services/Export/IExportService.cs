using alxnbl.OneNoteMdExporter.Models;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public interface IExportService
    {
        void ExportNotebook(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "");
    }
}