using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace alxnbl.OneNoteMdExporter
{
    public class Program
    {
        public static void Main()
        {
            var appSettings = AppSettings.LoadAppSettings();
            InitLogger();
            var onenoteApp = new Application();


            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

            Log.Debug($"CurrentCulture : {CultureInfo.CurrentCulture}");
            Log.Debug($"OneNoteMdExporter version {assemblyVersion}");

            var notebooks = onenoteApp.GetNotebooks();


            Log.Information("-----------------------");
            Log.Information("- OneNote Md Exporter -");
            Log.Information($"       v{assemblyVersion}");
            Log.Information("-----------------------\n");

            Log.Information(Localizer.GetString("NotebookFounds"), notebooks.Count);
            Log.Information(Localizer.GetString("ExportAllNotebooks"));

            for (int i=1; i<=notebooks.Count; i++)
            {
                Log.Information(Localizer.GetString("ExportNotebookPositionX"), i, notebooks.ElementAt(i-1).Title);
            }

            var input = Console.ReadLine();
            
            if(!Int32.TryParse(input, out var inputInt))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return;
            }

            IList<Notebook> notebookToProcess;

            if (inputInt == 0)
            {
                notebookToProcess = notebooks;
            }
            else
            {
                try
                {
                    notebookToProcess = new List<Notebook> { notebooks.ElementAt(inputInt - 1) };
                }
                catch (ArgumentOutOfRangeException)
                {
                    Log.Information(Localizer.GetString("NotebookNotFound"));
                    return;
                }
            }


            Log.Information(Localizer.GetString("ChooseExportFormat"));
            Log.Information(Localizer.GetString("ChooseExportFormat1"));
            Log.Information(Localizer.GetString("ChooseExportFormat2"));


            var exportFormatTxt = Console.ReadLine();


            Log.Information("");


            if (!Enum.TryParse<ExportFormat>(exportFormatTxt, true, out var exportFormat))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return;
            }

            var exportService = ExportServiceFactory.GetExportService(exportFormat, appSettings, onenoteApp);

            foreach (Notebook notebook in notebookToProcess)
            {
                Log.Information("\n***************************************");
                Log.Information(Localizer.GetString("StartExportingNotebook"), notebook.Title);
                Log.Information("***************************************");

                exportService.ExportNotebook(notebook);
                var exportPath = Path.GetFullPath(notebook.Title);

                Log.Information("");
                Log.Information(Localizer.GetString("ExportSuccessful"), exportPath);
                Log.Information("");
            }
        }

        private static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .WriteTo.File("OneNoteMdExporter.log")
               .WriteTo.Console(Serilog.Events.LogEventLevel.Information, "{Message:lj}{NewLine}")
               .CreateLogger();
        }
    }
}