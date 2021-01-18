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

namespace alxnbl.OneNoteMdExporter
{
    public class Program
    {
        public static void Main()
        {
            var appSettings = AppSettings.LoadAppSettings();
            InitLogger();
            var onenoteApp = new Application();

            Log.Debug($"CurrentCulture : {CultureInfo.CurrentCulture}");

            var notebooks = onenoteApp.GetNotebooks();

            Console.WriteLine(Localizer.GetString("NotebookFounds"), notebooks.Count);
            Console.WriteLine(Localizer.GetString("ExportAllNotebooks"));

            for (int i=1; i<=notebooks.Count; i++)
            {
                Console.WriteLine(Localizer.GetString("ExportNotebookPositionX"), i, notebooks.ElementAt(i-1).Title);
            }

            var input = Console.ReadLine();
            
            if(!Int32.TryParse(input, out var inputInt))
            {
                Console.WriteLine(Localizer.GetString("BadInput"));
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
                    Console.WriteLine(Localizer.GetString("NotebookNotFound"));
                    return;
                }
            }


            Console.WriteLine(Localizer.GetString("ChooseExportFormat"));
            Console.WriteLine(Localizer.GetString("ChooseExportFormat1"));
            Console.WriteLine(Localizer.GetString("ChooseExportFormat2"));


            var exportFormatTxt = Console.ReadLine();


            if (!Enum.TryParse<ExportFormat>(exportFormatTxt, true, out var exportFormat))
            {
                Console.WriteLine(Localizer.GetString("BadInput"));
                return;
            }

            var exportService = ExportServiceFactory.GetExportService(exportFormat, appSettings, onenoteApp);

            foreach (Notebook notebook in notebookToProcess)
            {
                Log.Information(Localizer.GetString("StartExportingNotebook"), notebook.Title);
                exportService.ExportNotebook(notebook);
                var exportPath = Path.GetFullPath(notebook.Title);
                Log.Information(Localizer.GetString("ExportSuccessful"), exportPath);
            }
        }

        private static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .WriteTo.File("consoleapp.log")
               .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
               .CreateLogger();
        }
    }
}