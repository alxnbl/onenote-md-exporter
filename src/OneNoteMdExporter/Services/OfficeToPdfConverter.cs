using alxnbl.OneNoteMdExporter.Infrastructure;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace alxnbl.OneNoteMdExporter.Services
{
    /// <summary>
    /// Long-term readable PDF copies of Office file attachments (.docx, .xlsx, .pptx, …), made with the
    /// installed Word / Excel / PowerPoint through late-bound COM, so no Office SDK is needed to build and
    /// the type does not even require Office to be installed to compile. Office runs invisibly; a
    /// password-protected file fails fast instead of prompting; every conversion has a timeout and a
    /// stuck Office instance is killed. The original Office file is always kept.
    /// </summary>
    public static class OfficeToPdfConverter
    {
        private static readonly string[] WordExt = [".doc", ".docx", ".docm", ".dot", ".dotx", ".rtf", ".odt", ".wpd", ".wps"];
        private static readonly string[] ExcelExt = [".xls", ".xlsx", ".xlsm", ".xlsb", ".xlt", ".xltx", ".ods"];
        private static readonly string[] PowerPointExt = [".ppt", ".pptx", ".pptm", ".pps", ".ppsx", ".pot", ".potx", ".odp"];

        private static dynamic word, excel, powerPoint;

        public static bool IsOfficeFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var allowed = AppSettings.OfficeToPdfExtensions ?? Array.Empty<string>();
            if (allowed.Length > 0 && !allowed.Any(a => string.Equals(a, ext, StringComparison.OrdinalIgnoreCase)))
                return false;
            return WordExt.Contains(ext) || ExcelExt.Contains(ext) || PowerPointExt.Contains(ext);
        }

        /// <summary>
        /// Writes "&lt;file&gt;.pdf" next to the Office file. Returns its path or null.
        /// Office runs invisibly; password-protected files fail fast instead of prompting.
        /// </summary>
        public static string OfficeToPdf(string filePath, List<string> warnings)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var pdfPath = filePath + ".pdf";
            var full = Path.GetFullPath(filePath);
            var fullPdf = Path.GetFullPath(pdfPath);

            var ok = RunWithTimeout(() =>
            {
                if (WordExt.Contains(ext))
                {
                    if (word == null) word = CreateOffice("Word.Application", "WINWORD", app => { app.Visible = false; app.DisplayAlerts = 0; });
                    var doc = word.Documents.Open(FileName: full, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false,
                        PasswordDocument: "#no-password#", Visible: false, OpenAndRepair: false, NoEncodingDialog: true);
                    try { doc.ExportAsFixedFormat(OutputFileName: fullPdf, ExportFormat: 17 /* wdExportFormatPDF */); }
                    finally { doc.Close(SaveChanges: 0); Marshal.FinalReleaseComObject(doc); }
                }
                else if (ExcelExt.Contains(ext))
                {
                    if (excel == null) excel = CreateOffice("Excel.Application", "EXCEL", app => { app.Visible = false; app.DisplayAlerts = false; app.AskToUpdateLinks = false; });
                    var wb = excel.Workbooks.Open(Filename: full, UpdateLinks: 0, ReadOnly: true, Password: "#no-password#", IgnoreReadOnlyRecommended: true, AddToMru: false);
                    try { wb.ExportAsFixedFormat(Type: 0 /* xlTypePDF */, Filename: fullPdf); }
                    finally { wb.Close(SaveChanges: false); Marshal.FinalReleaseComObject(wb); }
                }
                else if (PowerPointExt.Contains(ext))
                {
                    if (powerPoint == null) powerPoint = CreateOffice("PowerPoint.Application", "POWERPNT", app => { app.DisplayAlerts = 1; /* ppAlertsNone */ });
                    // "::#no-password#::" suffix makes a protected file fail instead of showing a dialog
                    var pres = powerPoint.Presentations.Open(FileName: full + "::#no-password#::", ReadOnly: -1, Untitled: 0, WithWindow: 0);
                    try { pres.SaveAs(FileName: fullPdf, FileFormat: 32 /* ppSaveAsPDF */); }
                    finally { pres.Close(); Marshal.FinalReleaseComObject(pres); }
                }
            }, out var error);

            if (ok && File.Exists(pdfPath))
                return pdfPath;

            warnings.Add($"PDF copy of '{Path.GetFileName(filePath)}' failed: {error ?? "no output"}");
            return null;
        }

        /// <summary>
        /// Office processes started by the export (process name =&gt; ids). Only these are closed at the end or
        /// killed after a timeout. PowerPoint runs as a single instance: when it was already open, the export
        /// borrows the user's window and must neither quit it nor kill it.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int[]> startedProcesses = new();

        private static int[] ProcessIds(string processName)
            => Process.GetProcessesByName(processName).Select(p => { try { return p.Id; } finally { p.Dispose(); } }).ToArray();

        private static dynamic CreateOffice(string progId, string processName, Action<dynamic> setup)
        {
            var type = Type.GetTypeFromProgID(progId) ?? throw new InvalidOperationException($"{progId} is not installed");
            var before = ProcessIds(processName);
            dynamic app = Activator.CreateInstance(type);
            var started = ProcessIds(processName).Except(before).ToArray();
            startedProcesses[processName] = started;
            if (started.Length == 0)
                Log.Debug($"{progId}: using an Office window that was already open; it will not be closed by the export");
            setup(app);
            return app;
        }

        /// <summary>
        /// A stuck Office (dialog open) is killed, else the next file reuses the same stuck instance (PowerPoint
        /// always does) and every following conversion waits for the timeout too.
        /// </summary>
        private static void KillStartedOffice()
        {
            foreach (var kv in startedProcesses)
            {
                foreach (var id in kv.Value)
                {
                    try { using var p = Process.GetProcessById(id); if (p.ProcessName.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) p.Kill(); }
                    catch { }
                }
            }
            startedProcesses.Clear();
        }

        /// <summary>
        /// Close the Office applications started by the export (safe to call when none were started).
        /// </summary>
        public static void CloseOffice()
        {
            if (officeWorker == null || (word == null && excel == null && powerPoint == null))
                return;

            RunWithTimeout(() =>
            {
                foreach (var (app, processName) in new (object, string)[] { (word, "WINWORD"), (excel, "EXCEL"), (powerPoint, "POWERPNT") })
                {
                    if (app == null) continue;
                    // Quit only an instance the export started (never the user's own PowerPoint window)
                    if (startedProcesses.TryGetValue(processName, out var ids) && ids.Length > 0)
                        try { ((dynamic)app).Quit(); } catch { }
                    try { Marshal.FinalReleaseComObject(app); } catch { }
                }
            }, out _);
            word = excel = powerPoint = null;
            startedProcesses.Clear();
            officeWorker?.Stop();
            officeWorker = null;
        }

        /// <summary>
        /// One long-lived STA thread owns the Office COM objects (a COM proxy may only be used from the apartment
        /// that created it). A call that does not return in time is abandoned together with its thread and Office
        /// instances; the next file gets a fresh thread and fresh Office instances.
        /// </summary>
        private sealed class StaWorker
        {
            private readonly System.Collections.Concurrent.BlockingCollection<Action> queue = new();
            public StaWorker()
            {
                var t = new Thread(() => { foreach (var a in queue.GetConsumingEnumerable()) a(); }) { IsBackground = true };
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
            public void Post(Action a) => queue.Add(a);
            public void Stop() => queue.CompleteAdding();
        }

        private static StaWorker officeWorker;

        private static bool RunWithTimeout(Action action, out string error)
        {
            officeWorker ??= new StaWorker();
            string err = null;
            using var done = new ManualResetEventSlim(false);
            officeWorker.Post(() =>
            {
                try { action(); }
                catch (Exception ex) { err = ex.InnerException?.Message ?? ex.Message; }
                finally { try { done.Set(); } catch (ObjectDisposedException) { } }
            });

            if (!done.Wait(TimeSpan.FromSeconds(Math.Max(10, AppSettings.ConversionTimeoutSeconds))))
            {
                error = "timeout (Office did not answer, a dialog may be open)";
                // Abandon the stuck thread and kill the Office instances it started (killing them also frees the thread)
                officeWorker.Stop();
                officeWorker = null;
                word = excel = powerPoint = null;
                KillStartedOffice();
                return false;
            }

            error = err;
            return err == null;
        }
    }
}
