using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using CommandLine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace alxnbl.OneNoteMdExporter
{
    public class Program
    {
        private const string loggerFilename = "logs.txt";

        public class Options
        {
            [Option('n', "notebook", Required = false, HelpText = "The name of the notebook to export")]
            public string NotebookName { get; set; }

            [Option('f', "format", Required = false, HelpText = "The format of export : 1 for Markdown folder ; 2 for Joplin folder")]
            public string ExportFormat { get; set; }

            [Option('s', "section", Required = false, HelpText = "The name of the section to export, apply only if notebook parameter used")]
            public string SectionName { get; set; }

            [Option('p', "page", Required = false, HelpText = "The name of the section page to export, apply only if section parameter used")]
            public string PageName { get; set; }

            [Option("no-input", Required = false, HelpText = "Do not request user input")]
            public bool NoInput { get; set; }

            [Option("all-notebooks", Required = false, HelpText = "Exports all notebooks, if specified with --notebook, notebook name is ignored")]
            public bool AllNotebooks { get; set; }

            [Option("debug", Required = false, HelpText = "Debug mode")]
            public bool Debug { get; set; }

            [Option("ignore-errors", Required = false, HelpText = "Export all notebook event in case of error")]
            public bool IgnoreErrors { get; set; }
        }

        public static void Main(params string[] args)
        {
            // Fix console encoding
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Run main app code
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(options =>
                   {
                       if (string.IsNullOrEmpty(options.NotebookName))
                           options.SectionName = string.Empty;
                       if (string.IsNullOrEmpty(options.SectionName))
                           options.PageName = string.Empty;

                       RunOptions(options);
                   });
        }

        private static void RunOptions(Options opts)
        {
            AppSettings.LoadAppSettings();
            AppSettings.Debug = opts.Debug;

            Log.Debug("Debug mode: {DebugMode}", AppSettings.Debug);
            InitLogger();

            try
            {
                AppDomain.CurrentDomain.ProcessExit += new EventHandler((_, _) => OneNoteApp.CleanUp());
                OneNoteApp.RenewInstance();
                Log.Debug("OneNote instance renewed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error communicating with OneNote");
                Log.Debug("Exception details: {Exception}", ex.ToString());

                if (!opts.NoInput && !opts.IgnoreErrors)
                {
                    Log.Information(Localizer.GetString("PressEnter"));
                    Console.ReadLine();
                }

                throw;
            }

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
                notebookToProcess = OneNoteApp.Instance.GetNotebooks();
            }

            if (notebookToProcess.Count == 0)
            {
                Thread.Sleep(3000);
                return;
            }

            ExportFormat exportFormat = ExportFormatSelectionForm(opts.ExportFormat);

            if (exportFormat == ExportFormat.Undefined)
            {
                Thread.Sleep(3000);
                return;
            }

            if (!opts.NoInput)
                UpdateSettingsForm();

            var exportService = ExportServiceFactory.GetExportService(exportFormat);

            foreach (Notebook notebook in notebookToProcess)
            {
                Log.Information("\n***************************************");
                Log.Information(Localizer.GetString("StartExportingNotebook"), notebook.Title);
                Log.Information("***************************************");

                var result = exportService.ExportNotebook(notebook, opts.SectionName, opts.PageName);

                if (!string.IsNullOrEmpty(result.NoteBookExportErrorMessage))
                {
                    // Unable to finalize notebook export
                    Log.Error(result.NoteBookExportErrorMessage);

                    if (!opts.NoInput)
                    {
                        Log.Information(Localizer.GetString("PressEnter"));
                        Console.ReadLine();
                    }
                }
                else if (result.PagesOnError > 0)
                {
                    Log.Information("");
                    Log.Warning(Localizer.GetString("ExportEndedWithErrors"), Path.GetFullPath(notebook.ExportFolder), result.PagesOnError, loggerFilename);
                    Log.Information("");

                    if (!opts.NoInput)
                    {
                        Log.Information(Localizer.GetString("PressEnter"));
                        Console.ReadLine();
                    }
                }
                else
                {
                    Log.Information("");
                    Log.Information(Localizer.GetString("ExportSuccessful"), Path.GetFullPath(notebook.ExportFolder));
                    Log.Information("");
                }
            }

            if (!AppSettings.Debug && !AppSettings.KeepOneNoteTempFiles)
            {
                TemporaryNotebook.CleanUp();
            }

            OneNoteApp.CleanUp();

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

            if (string.Equals(editSettings, Localizer.GetString("YesAnswer"), StringComparison.CurrentCultureIgnoreCase))
            {
                var process = new Process();
                process.StartInfo.FileName = "notepad.exe";
                process.StartInfo.Arguments = Path.GetFullPath("appSettings.json");
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
            Console.BackgroundColor = ConsoleColor.DarkMagenta;
            Log.Information(Localizer.GetString("WelcomeMessageWarn"));
            Console.BackgroundColor = ConsoleColor.Black;
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

            ExportFormat exportFormat;

            // Select 1st option by default
            if (optsExportFormat == "")
            {
                exportFormat = ExportFormat.Markdown;
                Console.CursorTop -= 2;
                Console.WriteLine("1\n");
            }
            else if (!Enum.TryParse(optsExportFormat, true, out exportFormat))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return ExportFormat.Undefined;
            }

            Log.Debug($"Format chosen: {exportFormat}");

            return exportFormat;
        }

        private static List<Notebook> GetNotebookFromName(string notebookName)
        {
            var notebook = OneNoteApp.Instance.GetNotebooks().Where(n => n.Title == notebookName).ToList(); // can be optimized

            if (notebook.Count == 0)
                Log.Information(Localizer.GetString("NotebookNameNotFound"), notebookName);

            return notebook;
        }

        private static IList<Notebook> NotebookSelectionForm()
        {
            var notebooks = OneNoteApp.Instance.GetNotebooks();

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
            var inputInt = input.Split(",").Select(s => int.TryParse(s, out var notebookNbr) ? notebookNbr : -1).Where(i => i >= 0).ToList();

            if (inputInt.Contains(0))
            {
                return notebooks;
            }
            else
            {
                var notebooksResult = inputInt.Where(i => i <= notebooks.Count).Select(i => notebooks.ElementAt(i - 1)).ToList();

                if (notebooksResult.Count == 0)
                    Log.Information(Localizer.GetString("NotebookNotFound"));

                return notebooksResult;
            }
        }

        private static void InitLogger()
        {
            var logLevel = AppSettings.Debug ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information;
            
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Is(logLevel)  // Imposta il livello minimo per tutti i sink
               .WriteTo.File(loggerFilename, restrictedToMinimumLevel: logLevel, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
               .WriteTo.Console(restrictedToMinimumLevel: logLevel, outputTemplate: "{Message:lj}{NewLine}")
               .CreateLogger();

            Log.Debug("Logger initialized with level: {LogLevel}", logLevel);
        }
    }
}