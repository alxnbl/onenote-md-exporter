using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Per-notebook journal of completely exported pages, used to resume an interrupted export.
    /// A page is written to the journal only after its md file and attachments are all on disk.
    /// File: &lt;notebook folder&gt;\.export-journal.tsv, one line per page: OneNoteId TAB relative md path.
    /// </summary>
    public class ExportJournal
    {
        public const string FileName = ".export-journal.tsv";

        private readonly string journalPath;
        private readonly string notebookFolder;
        private readonly Dictionary<string, string> completed = new(StringComparer.Ordinal);

        public int ResumedPageCount => completed.Count;

        public ExportJournal(string notebookFolder, bool resume)
        {
            this.notebookFolder = notebookFolder;
            journalPath = Path.Combine(notebookFolder, FileName);

            if (resume && File.Exists(journalPath))
            {
                var lines = File.ReadAllLines(journalPath)
                    .Select(l => l.Split('\t'))
                    .Where(p => p.Length == 2 && p[0].Length > 0 && p[1].Length > 0) // ignore a half-written last line
                    .ToList();

                // The last recorded page is exported again on purpose: cheap insurance against a stop right after it
                if (lines.Count > 0)
                    lines.RemoveAt(lines.Count - 1);

                foreach (var p in lines)
                    completed[p[0]] = p[1];

                // Two pages recorded with the SAME file means one overwrote the other (possible when page paths
                // were not unique across sections). Export both again, or the one that was overwritten stays lost.
                var shared = completed.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
                foreach (var g in shared)
                {
                    Log.Warning($"Resume: {g.Count()} pages were written to the same file '{g.Key}'; they are exported again");
                    foreach (var kv in g.ToList())
                        completed.Remove(kv.Key);
                }

                // Rewrite the journal without the dropped entries
                File.WriteAllLines(journalPath, completed.Select(kv => $"{kv.Key}\t{kv.Value}"));

                Log.Information($"Resuming export: {completed.Count} page(s) already exported will be skipped");
            }
            else if (File.Exists(journalPath))
            {
                File.Delete(journalPath);
            }
        }

        /// <summary>
        /// True if the page was completely exported in a previous run and its md file still exists
        /// </summary>
        public bool IsCompleted(Page page, string mdFilePath)
        {
            if (page.OneNoteId == null || !completed.TryGetValue(page.OneNoteId, out var relPath))
                return false;

            var expected = Path.GetRelativePath(notebookFolder, mdFilePath);
            if (!string.Equals(relPath, expected, StringComparison.OrdinalIgnoreCase))
                return false; // page moved or renamed since last run => export it again

            return File.Exists(mdFilePath);
        }

        public void MarkCompleted(Page page, string mdFilePath)
        {
            if (page.OneNoteId == null) return;
            var rel = Path.GetRelativePath(notebookFolder, mdFilePath);
            completed[page.OneNoteId] = rel;
            File.AppendAllLines(journalPath, new[] { $"{page.OneNoteId}\t{rel}" });
        }
    }
}
