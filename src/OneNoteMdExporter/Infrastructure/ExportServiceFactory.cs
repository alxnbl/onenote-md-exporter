using alxnbl.OneNoteMdExporter.Models;
using alxnbl.OneNoteMdExporter.Services.Export;
using System;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    public static class ExportServiceFactory
    {
        public static IExportService GetExportService(ExportFormat exportFormat) => exportFormat switch
        {
            ExportFormat.Markdown => new MdExportService(),
            ExportFormat.JoplinMdFolder => new JoplinExportService(),
            _ => throw new NotImplementedException(),
        };
    }
}
