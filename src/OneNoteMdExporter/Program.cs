using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using CommandLine;
using OneNote = Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace alxnbl.OneNoteMdExporter
{
    public class Program
    {
        public class Options
        {
            [Option('n', "notebook", Required = false, HelpText = "The name of the notebook to export")]
            public string NotebookName { get; set; }

            [Option('f', "format", Required = false, HelpText = "The format of export : 1 for Markdown folder ; 2 for Joplin folder")]
            public string ExportFormat { get; set; }

            [Option('s', "section", Required = false, HelpText = "The name of the section to export. Apply only if notebook parameter used.")]
            public string SectionName { get; set; }

            [Option('p', "page", Required = false, HelpText = "The name of the section page to export. Apply only if section parameter used.")]
            public string PageName { get; set; }

            [Option("no-input", Required = false, HelpText = "Do not request user input")]
            public bool NoInput { get; set; }

            [Option("all-notebooks", Required = false, HelpText = "Exports all notebooks, if specifed with --notebook, notebook name is ignored.")]
            public bool AllNotebooks { get; set; }

            [Option("debug", Required = false, HelpText = "Debug mode.")]
            public bool Debug { get; set; }
        }

        public static void Main(params string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(options => {
                       if (string.IsNullOrEmpty(options.NotebookName))
                           options.SectionName = string.Empty;
                       if (string.IsNullOrEmpty(options.SectionName))
                           options.PageName = string.Empty;

                       RunOptions(options);
                    });
        }

        private static OneNote.Application OneNoteApp;

        private static void RunOptions(Options opts)
        {
            var appSettings = AppSettings.LoadAppSettings();
            appSettings.Debug = opts.Debug;

            InitLogger();

            OneNoteApp = new OneNote.Application();

            WelcomeScreen(opts);

            IList<Notebook> notebookToProcess;

            if (!opts.AllNotebooks)
            {
                if (string.IsNullOrEmpty(opts.NotebookName))
                {
                    // Request user to select notebooks to export
                    notebookToProcess = NotebookSelectionForm();
                }
                else
                {
                    // Notebook name provided in args
                    notebookToProcess = GetNotebookFromName(opts.NotebookName);
                }
            }
            else
            {
                // if all-notebooks specified get all notebooks.
                notebookToProcess = OneNoteApp.GetNotebooks();
            }

            if (notebookToProcess.Count == 0)
                return;

            ExportFormat exportFormat = ExportFormatSelectionForm(opts.ExportFormat);

            if (exportFormat == ExportFormat.Undefined)
                return;

            if (!opts.NoInput)
                UpdateSettingsForm();

            var exportService = ExportServiceFactory.GetExportService(exportFormat, appSettings, OneNoteApp);

            foreach (Notebook notebook in notebookToProcess)
            {
                Log.Information("\n***************************************");
                Log.Information(Localizer.GetString("StartExportingNotebook"), notebook.Title);
                Log.Information("***************************************");

                exportService.ExportNotebook(notebook, opts.SectionName, opts.PageName);

                Log.Information("");
                Log.Information(Localizer.GetString("ExportSuccessful"), Path.GetFullPath(notebook.ExportFolder));
                Log.Information("");
            }

            if (!opts.NoInput)
            {
                if (notebookToProcess.Count == 1)
                    Process.Start("explorer.exe", Path.GetFullPath(notebookToProcess.First().ExportFolder) + Path.DirectorySeparatorChar);

                Log.Information(Localizer.GetString("EndOfExport"));
                Console.ReadLine();
            }
        }

        private static void UpdateSettingsForm()
        {
            Log.Information(Localizer.GetString("DoYouWantToUpdateSettings"));

            var editSettings = Console.ReadLine();

            string TEMP = editSettings.ToLower();
            if (TEMP.Equals(Localizer.GetString("YesAnswer")))
            {
                var process = new Process();
                process.StartInfo.FileName = "notepad.exe";
                process.StartInfo.Arguments = Path.GetFullPath("appsettings.json");
                process.Start();
                process.WaitForExit();
            }
        }

        private static void WelcomeScreen(Options opts)
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

            Log.Debug($"CurrentCulture : {CultureInfo.CurrentCulture}");
            Log.Debug($"OneNoteMdExporter version {assemblyVersion}");

            Log.Information("-----------------------");
            Log.Information("- OneNote Md Exporter -");
            Log.Information($"       v{assemblyVersion}");
            Log.Information("-----------------------\n");

            Log.Information(Localizer.GetString("WelcomeMessage"));
            Log.Information(Localizer.GetString("PressEnter"));


            if (!opts.NoInput)
                Console.ReadLine();
        }

        private static ExportFormat ExportFormatSelectionForm(string optsExportFormat = "")
        {
            if (string.IsNullOrEmpty(optsExportFormat))
            {
                Log.Information(Localizer.GetString("ChooseExportFormat"));
                Log.Information(Localizer.GetString("ChooseExportFormat1"));
                Log.Information(Localizer.GetString("ChooseExportFormat2"));

                optsExportFormat = Console.ReadLine();

                Log.Information("");
            }


            if (!Enum.TryParse<ExportFormat>(optsExportFormat, true, out var exportFormat))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return ExportFormat.Undefined;
            }

            Log.Debug($"Format choosen: {exportFormat}");

            return exportFormat;
        }

        private static IList<Notebook> GetNotebookFromName(string notebookName)
        {
            var notebook = OneNoteApp.GetNotebooks().Where(n => n.Title == notebookName).ToList(); // can be optimized

            if(notebook.Count == 0)
                Log.Information(Localizer.GetString("NotebookNameNotFound"), notebookName);

            return notebook;
        }

        private static IList<Notebook> NotebookSelectionForm()
        {
            var notebooks = OneNoteApp.GetNotebooks();

            Log.Information("\n***************************************");
            Log.Information(Localizer.GetString("NotebookFounds"), notebooks.Count);
            Log.Information("***************************************\n");

            Log.Information(Localizer.GetString("PleaseChooseNotebookToExport"));
            
            Log.Information(Localizer.GetString("ExportAllNotebooks"));

            for (int i = 1; i <= notebooks.Count; i++)
            {
                Log.Information(Localizer.GetString("ExportNotebookPositionX"), i, notebooks.ElementAt(i - 1).Title);
            }

            var input = Console.ReadLine();
            var inputInt = input.Split(",").Select(s => Int32.TryParse(s, out var notebookNbr) ? notebookNbr : -1).Where(i => i >= 0).ToList();

            if (inputInt.Contains(0))
                return notebooks;
            else
            {
                var notebooksResult = inputInt.Where(i => i <= notebooks.Count).Select(i => notebooks.ElementAt(i - 1)).ToList();

                if (!notebooksResult.Any())
                    Log.Information(Localizer.GetString("NotebookNotFound"));

                return notebooksResult;
            }

        }

        private static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .WriteTo.File("logs.txt", outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
               .WriteTo.Console(Serilog.Events.LogEventLevel.Information, "{Message:lj}{NewLine}")
               .CreateLogger();
        }
    }
}