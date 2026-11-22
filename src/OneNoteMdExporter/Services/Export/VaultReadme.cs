using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Writes VAULT.md at the export root: a plain-text description of the archive, so the export can be
    /// understood later without this tool. The "Export log" table at the end gets one row per notebook run.
    /// Only the features that are actually turned on are described.
    /// </summary>
    public static class VaultReadme
    {
        public const string FileName = "VAULT.md";

        public static void Write(string exportRoot, Notebook notebook, NotebookExportResult result)
        {
            var path = Path.Combine(Path.GetFullPath(exportRoot), FileName);

            if (!File.Exists(path))
                File.WriteAllText(path, BuildHeader(), new UTF8Encoding(false));

            var row = $"| {DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)} | {Escape(notebook.Title)} | {result.PagesExported} | {result.PagesOnError} | {Version()} |";
            File.AppendAllText(path, row + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string Escape(string s) => (s ?? "").Replace("|", "\\|");

        private static string Version()
            => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
               ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        private static string BuildHeader()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# OneNote archive");
            sb.AppendLine();
            sb.AppendLine("This folder is an export of Microsoft OneNote notebooks into open formats, made to stay readable without OneNote.");
            sb.AppendLine($"It was produced by OneNoteMdExporter {Version()} (github.com/alxnbl/onenote-md-exporter).");
            sb.AppendLine();

            sb.AppendLine("## Layout");
            sb.AppendLine();
            sb.AppendLine("- One folder per notebook, then one folder per section group and section, mirroring OneNote.");
            sb.AppendLine("- Each OneNote page is a Markdown file (`.md`). Sub-pages sit in a folder named after their parent page.");
            sb.AppendLine($"- Images and attached files are in the `{AppSettings.ResourceFolderName}` folder{(AppSettings.ResourceFolderLocation == ResourceFolderLocationEnum.PageParentFolder ? " next to the pages that use them" : " at the root of each notebook")}.");
            if (AppSettings.ConvertLegacyFormats)
                sb.AppendLine("- EMF/WMF images and XPS printout attachments were also written as PNG (which Markdown viewers can show); the originals are kept next to the PNGs.");
            if (AppSettings.ConvertOfficeToPdf)
                sb.AppendLine("- Office file attachments have a PDF copy (`<file>.pdf`) next to the original.");
            if (!string.IsNullOrWhiteSpace(AppSettings.FfmpegPath) &&
                ((AppSettings.AudioToMp3Extensions?.Length ?? 0) > 0 || (AppSettings.VideoToMp4Extensions?.Length ?? 0) > 0))
                sb.AppendLine("- Audio/video attachments have an MP3 or MP4 copy next to the original.");
            if (AppSettings.WriteIndexNotes)
                sb.AppendLine("- `_index.md` in each section lists its pages in OneNote order; `_index.md` in each notebook lists its sections.");
            sb.AppendLine($"- `{ExportJournal.FileName}` (hidden) records completed pages, used only to resume an interrupted export.");
            if (AppSettings.MaxPathLength > 0)
                sb.AppendLine("- Pages whose path would be too long for Windows have a shortened name ending in `~hash`, or sit in `_long-paths` in the notebook folder.");
            sb.AppendLine();

            if (AppSettings.AddFrontMatterHeader)
            {
                sb.AppendLine("## Page metadata");
                sb.AppendLine();
                sb.AppendLine("Each `.md` starts with a YAML header with `title`, `created` and `updated`, taken from OneNote.");
                sb.AppendLine();
            }

            sb.AppendLine("## Known limits");
            sb.AppendLine();
            sb.AppendLine("- Free-form page layout (side-by-side boxes) is flattened top to bottom in the `.md`.");
            sb.AppendLine("- Password-protected sections are only exported if they were unlocked during the export.");
            sb.AppendLine(AppSettings.RepairLinks
                ? "- Links between pages are plain relative links; a link into another notebook of this archive works once that notebook is exported. A link to a page outside the archive is marked `(OneNote link to a page not in this archive)`."
                : "- Links to other pages are kept where the target page was already exported when the link was written; forward links may be reduced to their text (turn on RepairLinks to resolve them all).");
            sb.AppendLine();

            sb.AppendLine("## Settings used");
            sb.AppendLine();
            sb.AppendLine($"Markdown flavour: `{AppSettings.PanDocMarkdownFormat}`. Links: `{AppSettings.OneNoteLinksHandling}`. Max file name length: {AppSettings.MdMaxFileLength}. Max path length: {(AppSettings.MaxPathLength > 0 ? AppSettings.MaxPathLength.ToString() : "off")}.");
            sb.AppendLine();

            sb.AppendLine("## Export log");
            sb.AppendLine();
            sb.AppendLine("| Date | Notebook | Exported | Failed | Version |");
            sb.AppendLine("|---|---|---|---|---|");
            return sb.ToString();
        }
    }
}
