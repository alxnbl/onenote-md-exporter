using alxnbl.OneNoteMdExporter.Models;
using alxnbl.OneNoteMdExporter.Services;
using alxnbl.OneNoteMdExporter.Services.Export;
using Microsoft.Office.Interop.OneNote;
using System;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    public static class ExportServiceFactory
    {
        public static IExportService GetExportService(ExportFormat exportFormat, AppSettings appSettings, Application oneNoteApp)
        {
            switch (exportFormat)
            {
                case ExportFormat.MdFolder:
                    return new MdExportService(appSettings, oneNoteApp, new ConverterService(appSettings));
                case ExportFormat.JoplinMdFolder:
                    return new JoplinExportService(appSettings, oneNoteApp, new ConverterService(appSettings));
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
