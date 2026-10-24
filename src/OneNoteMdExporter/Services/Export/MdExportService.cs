using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Markdown exporter Service
    /// </summary>
    public class MdExportService : ExportServiceBase
    {
        protected override string ExportFormatCode { get; } = "md";

        protected override string GetResourceFolderPath(Page page)
        {
            if (AppSettings.ResourceFolderLocation == ResourceFolderLocationEnum.RootFolder)
                return Path.Combine(page.GetNotebook().ExportFolder, AppSettings.ResourceFolderName);
            else
                return Path.Combine(Path.GetDirectoryName(GetPageMdFilePath(page)), AppSettings.ResourceFolderName);
        }

        protected override string GetPageMdFilePath(Page page)
        {
            if (page.OverridePageFilePath == null)
            {
                var defaultPath = Path.Combine(page.GetNotebook().ExportFolder, page.GetPageFileRelativePath(AppSettings.MdMaxFileLength) + ".md");

                if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsFolderTree)
                {
                    if (page.ParentPage != null)
                        return Path.Combine(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsPageTitlePrefix)
                {
                    if (page.ParentPage != null)
                        return String.Concat(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), AppSettings.PageHierarchyFileNamePrefixSeparator, page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else
                    return defaultPath;
            }
            else
            {
                return page.OverridePageFilePath;
            }
        }

        protected override string GetAttachmentFilePath(Attachement attachment)
        {
            if (attachment.OverrideExportFilePath == null)
                return Path.Combine(GetResourceFolderPath(attachment.ParentPage), attachment.Id + Path.GetExtension(attachment.FriendlyFileName));
            else
                return attachment.OverrideExportFilePath;
        }

        /// <summary>
        /// Get relative path from Image's folder to attachment folder
        /// </summary>
        /// <param name="attachment"></param>
        /// <returns></returns>
        protected override string GetAttachmentMdReference(Attachement attachment)
            => Path.GetRelativePath(Path.GetDirectoryName(GetPageMdFilePath(attachment.ParentPage)), GetAttachmentFilePath(attachment)).Replace("\\", "/");

        public override NotebookExportResult ExportNotebookInTargetFormat(Notebook notebook, string sectionNameFilter = "", string pageNameFilter = "")
        {
            var result = new NotebookExportResult();

            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections().Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information(String.Format(Localizer.GetString("FoundXSections"), sections.Count));

            // Phase 1: Build complete tree and collect metadata
            Log.Information(Localizer.GetString("NotebookProcessingStartingPhase1"));
            var allPages = new List<Page>();
            
            // Export each section
            int cmptSect = 0;
            foreach (Section section in sections)
            {
                Log.Information($"- {Localizer.GetString("Section")} ({++cmptSect}/{sections.Count}) :  {section.GetPath(AppSettings.MdMaxFileLength)}\\{section.Title}");

                if (section.IsSectionGroup)
                    throw new InvalidOperationException("Cannot call ExportSection on section group with MdExport");

                // Get pages list and collect metadata
                var pages = OneNoteApp.Instance.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();
                allPages.AddRange(pages);
            }

            // Phase 2: Export content and convert to markdown
            Log.Information("\n" + Localizer.GetString("NotebookProcessingStartingPhase2"));
            int cmptPage = 0;
            foreach (Page page in allPages)
            {
                Log.Information($"- {Localizer.GetString("Page")} {++cmptPage}/{allPages.Count} : {page.Parent.Title} / {page.TitleWithPageLevelTabulation}");
                var success = ExportPage(page);

                if (!success) result.PagesOnError++;
            }

            // Record this notebook's page id => file map, used by LinkRepair after the whole run
            if (AppSettings.RepairLinks)
                LinkRepair.WriteLinkMap(notebook, allPages, GetPageMdFilePath, GetNotebookFolderPath(notebook));

            // Write _index.md per section (its pages) and per notebook (its sections)
            if (AppSettings.WriteIndexNotes)
            {
                try { WriteIndexNotes(notebook, sections, allPages); }
                catch (Exception ex) { Log.Warning($"Writing index notes for '{notebook.Title}' failed: {ex.Message}"); }
            }

            return result;
        }

        private static string YamlQuote(string value) => "'" + (value ?? "").Replace("'", "''") + "'";

        private static string YamlUnquote(string value)
        {
            var v = (value ?? "").Trim();
            if (v.Length >= 2 && v[0] == '\'' && v[^1] == '\'')
                return v[1..^1].Replace("''", "'");
            return v;
        }

        /// <summary>
        /// Write "_index.md" in each section folder (its pages in OneNote order, indented by page level) and one
        /// at the notebook root listing the sections. A run that exports only some sections (or resumes) keeps the
        /// sections already on disk in the notebook index, read back from their front matter.
        /// </summary>
        private void WriteIndexNotes(Notebook notebook, IList<Section> sections, List<Page> allPages)
        {
            var notebookFolder = Path.GetFullPath(GetNotebookFolderPath(notebook));
            var pagesBySection = allPages.GroupBy(p => p.Parent).ToDictionary(g => g.Key, g => g.OrderBy(p => p.PageSectionOrder).ToList());
            var sectionEntries = new List<(string path, string label, int pages)>();

            foreach (var section in sections)
            {
                if (!pagesBySection.TryGetValue(section, out var pages) || pages.Count == 0)
                    continue;

                var sectionFolder = Path.GetFullPath(Path.Combine(notebook.ExportFolder, section.GetPath(AppSettings.MdMaxFileLength)));
                Directory.CreateDirectory(sectionFolder);
                var indexPath = Path.Combine(sectionFolder, "_index.md");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("---");
                sb.AppendLine($"title: {YamlQuote(section.Title)}");
                sb.AppendLine("type: index");
                sb.AppendLine($"notebook: {YamlQuote(notebook.Title)}");
                sb.AppendLine($"sectionPath: {YamlQuote(section.GetPath(255).Replace('\\', '/'))}");
                sb.AppendLine($"pages: {pages.Count}");
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"# {section.Title}");
                sb.AppendLine();

                foreach (var page in pages)
                {
                    var mdPath = Path.GetFullPath(GetPageMdFilePath(page));
                    var exists = File.Exists(mdPath);
                    var rel = LinkRepairCore.ToMdLinkPath(Path.GetRelativePath(sectionFolder, mdPath));
                    var indent = new string(' ', Math.Max(0, page.PageLevel - 1) * 2);
                    var date = page.CreationDate.ToString("yyyy-MM-dd");
                    var title = string.IsNullOrWhiteSpace(page.Title) ? "(untitled)" : page.Title.Replace("[", "\\[").Replace("]", "\\]");

                    sb.AppendLine(exists ? $"{indent}- [{title}]({rel}) · {date}" : $"{indent}- {title} · {date} (not exported)");
                }

                File.WriteAllText(indexPath, sb.ToString());
                sectionEntries.Add((indexPath, section.GetPath(255).Replace('\\', '/'), pages.Count));
            }

            // A run that exports one section (or resumes) must not shrink the notebook index to that section:
            // every section _index.md already on disk counts, read back from its front matter.
            foreach (var indexPath in Directory.EnumerateFiles(notebookFolder, "_index.md", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(indexPath)), notebookFolder, StringComparison.OrdinalIgnoreCase))
                    continue; // the notebook index itself
                if (sectionEntries.Any(e => string.Equals(e.path, Path.GetFullPath(indexPath), StringComparison.OrdinalIgnoreCase)))
                    continue;
                try
                {
                    string label = null; int count = 0;
                    foreach (var line in File.ReadLines(indexPath).Take(12))
                    {
                        if (line.StartsWith("sectionPath: ")) label = YamlUnquote(line["sectionPath: ".Length..]);
                        else if (line.StartsWith("pages: ")) int.TryParse(line["pages: ".Length..].Trim(), out count);
                    }
                    if (label != null)
                        sectionEntries.Add((Path.GetFullPath(indexPath), label, count));
                }
                catch (Exception ex) { Log.Debug($"Could not read {indexPath}: {ex.Message}"); }
            }

            var nb = new System.Text.StringBuilder();
            nb.AppendLine("---");
            nb.AppendLine($"title: {YamlQuote(notebook.Title)}");
            nb.AppendLine("type: index");
            nb.AppendLine($"sections: {sectionEntries.Count}");
            nb.AppendLine($"pages: {sectionEntries.Sum(e => e.pages)}");
            nb.AppendLine("---");
            nb.AppendLine();
            nb.AppendLine($"# {notebook.Title}");
            nb.AppendLine();
            foreach (var (path, label, count) in sectionEntries.OrderBy(e => e.label, StringComparer.CurrentCultureIgnoreCase))
            {
                var rel = LinkRepairCore.ToMdLinkPath(Path.GetRelativePath(notebookFolder, path));
                nb.AppendLine($"- [{label}]({rel}) · {count} pages");
            }
            Directory.CreateDirectory(notebookFolder);
            File.WriteAllText(Path.Combine(notebookFolder, "_index.md"), nb.ToString());
        }

        protected override void WritePageMdFile(Page page, string pageMd)
        {
            File.WriteAllText(GetPageMdFilePath(page), pageMd);
        }

        protected override void FinalizeExportPageAttachments(Page page, Attachement attachment)
        {
            return; // No markdown file generated for attachments
        }

        protected override void PrepareFolders(Page page)
        {
            var pageDirectory = Path.GetDirectoryName(GetPageMdFilePath(page));

            if (!Directory.Exists(pageDirectory))
                Directory.CreateDirectory(pageDirectory);
        }

        protected override string FinalizePageMdPostProcessing(Page page, string md)
        {
            var res = md;

            if (AppSettings.AddFrontMatterHeader)
                res = AddFrontMatterHeader(page, md);

            return res;
        }

        protected override string GetPageWikilink(string linkText, string mdFilePath, string pageId)
        {
            var normalizedPath = mdFilePath.Replace('\\', '/');

            if (AppSettings.OneNoteLinksHandling == OneNoteLinksHandlingEnum.ConvertToWikilink)
            {
                // For Wikilinks, we use the format [[MdFilePath]] or [[MdFilePath|Display Text]]
                return normalizedPath == linkText ?
                    $"[[{normalizedPath}]]" :
                    $"[[{normalizedPath}|{linkText}]]";
            }
            else // ConvertToMarkdown
            {
                normalizedPath = normalizedPath.Replace(" ", "%20");
                return $"[{linkText}]({normalizedPath}.md)";
            }
        }

        private static string AddFrontMatterHeader(Page page, string pageMd)
        {
            var headerModel = new FrontMatterHeader
            {
                Title = page.Title,
                Created = page.CreationDate,
                Updated = page.LastModificationDate
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTypeConverter(new DateTimeConverter(formats: AppSettings.FrontMatterDateFormat, kind: DateTimeKind.Local))
                .Build();
            var headerYaml = serializer.Serialize(headerModel);

            return "---\n" + headerYaml + "---\n\n" + pageMd;
        }

        private class FrontMatterHeader
        {
            public string Title { get; set; }
            public DateTime Updated { get; set; }
            public DateTime Created { get; set; }
        }
    }

}
