using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Resolve links between exported pages.
    ///
    /// With RepairLinks on, a link to another OneNote page is written during export as a placeholder
    /// "[text](onenote-page:&lt;id&gt;)", because the target page's final file may not exist yet (it may be in a
    /// sub-page folder, renamed to stay unique, path-shortened, or in a notebook exported later). Each notebook
    /// writes a .link-map.tsv (page id =&gt; md file). After the run, every md file under the export root is scanned
    /// and the placeholders are replaced by plain relative links that work in any tool. A link to a page that is
    /// not in the archive is left as a placeholder with a note, and a later run that exports its notebook into the
    /// same root resolves it.
    /// </summary>
    public static class LinkRepair
    {
        public const string MapFileName = ".link-map.tsv";

        /// <summary>
        /// Write the notebook's page id =&gt; md file map, used by <see cref="RepairAll"/> after the run.
        /// </summary>
        public static void WriteLinkMap(Notebook notebook, IEnumerable<Page> pages, Func<Page, string> getPageMdFilePath, string notebookFolder)
        {
            var root = Path.GetFullPath(notebook.ExportFolder);

            // page node id => programmatic id (the id that appears in onenote: links and in the placeholders)
            var idByNode = OneNoteLinkTranslatorService.PageMetadata.Values
                .Where(m => m.NodeId != null && m.ProgrammaticId != null)
                .GroupBy(m => m.NodeId)
                .ToDictionary(g => g.Key, g => g.First().ProgrammaticId);

            var lines = new List<string>();
            foreach (var page in pages)
            {
                if (!idByNode.TryGetValue(page.Id, out var linkId)) continue;
                var md = Path.GetFullPath(getPageMdFilePath(page));
                lines.Add($"page\t{linkId.ToLowerInvariant()}\t{Path.GetRelativePath(root, md)}");
            }

            Directory.CreateDirectory(notebookFolder);
            File.WriteAllLines(Path.Combine(notebookFolder, MapFileName), lines, new UTF8Encoding(false));
        }

        /// <summary>
        /// Resolve placeholders in every md file under the export roots of the given notebooks (including notebooks
        /// a previous run exported into the same root).
        /// </summary>
        public static void RepairAll(IEnumerable<Notebook> notebooks)
        {
            foreach (var root in notebooks.Where(n => n.ExportFolder != null)
                         .Select(n => Path.GetFullPath(n.ExportFolder))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root)) continue;
                try { RepairRoot(root); }
                catch (Exception ex) { Log.Warning($"Link repair failed in '{root}': {ex.Message}"); }
            }
        }

        /// <summary>Resolve placeholders under one export root. Internal so it can be unit-tested with plain files.</summary>
        internal static void RepairRoot(string root)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapFile in Directory.EnumerateFiles(root, MapFileName, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
            {
                foreach (var line in File.ReadAllLines(mapFile))
                {
                    var p = line.Split('\t');
                    if (p.Length == 3) map[$"{p[0]}:{p[1]}"] = Path.Combine(root, p[2]);
                }
            }
            if (map.Count == 0) return;

            int files = 0, resolved = 0, unresolved = 0;
            foreach (var md in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(md);
                if (!text.Contains("onenote-page:"))
                    continue;

                var dir = Path.GetDirectoryName(md);
                var changed = LinkRepairCore.ResolveText(text, map, dir, out var r, out var u);
                resolved += r;
                unresolved += u;

                if (changed != text)
                {
                    // Keep the file dates (they carry the OneNote dates)
                    var created = File.GetCreationTime(md);
                    var modified = File.GetLastWriteTime(md);
                    File.WriteAllText(md, changed, new UTF8Encoding(false));
                    File.SetCreationTime(md, created);
                    File.SetLastWriteTime(md, modified);
                    files++;
                }
            }

            Log.Information($"Links: {resolved} link(s) between pages repaired in {files} file(s), {unresolved} point(s) to pages outside this archive");
        }
    }
}
