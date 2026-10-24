using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    internal class OneNoteLinkTranslatorService
    {

        // Dictionary to store page and section mappings
        public static readonly Dictionary<string, OneNoteLinkMetadata> PageMetadata = new();
        //private static readonly Dictionary<string, OneNoteLinkMetadata> SectionMetadata = new();

        /// <summary>
        /// Register a page mapping for link conversion; the key is the programmatic ID generated from OneNote
        /// </summary>
        /// <param name="pageId">OneNote page ID</param>
        /// <param name="programmaticId">Programmatic ID generated from OneNote</param>
        /// <param name="exportPath">Relative export path of the page</param>
        /// <param name="title">Page title</param>
        public void RegisterPageMapping(string nodeId, string pageId, string programmaticId, string exportPath, string title)
        {
            PageMetadata[programmaticId] = new OneNoteLinkMetadata
            {
                NodeId = nodeId,
                OriginalId = pageId,
                ProgrammaticId = programmaticId,
                MdFilePath = exportPath,
                Title = title
            };
        }

        ///// <summary>
        ///// Register a section mapping for link conversion; the key is the programmatic ID generated from OneNote
        ///// </summary>
        ///// <param name="sectionId">OneNote section ID</param>
        ///// <param name="programmaticId">Programmatic ID generated from OneNote</param>
        ///// <param name="exportPath">Relative export path of the section</param>
        ///// <param name="title">Section title</param>
        //public static void RegisterSectionMapping(string sectionId, string programmaticId, string exportPath, string title)
        //{
        //    SectionMetadata[programmaticId] = new OneNoteLinkMetadata
        //    {
        //        OriginalId = sectionId,
        //        ProgrammaticId = programmaticId,
        //        MdFilePath = exportPath,
        //        Title = title
        //    };
        //}



        public void initializePage(Page page, string pagePath)
        {
            string pageProgrammaticId = null;
            try
            {
                OneNoteApp.Instance.GetHyperlinkToObject(page.OneNoteId, null, out string pageLink);
                var pageIdMatch = Regex.Match(pageLink, @"page-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                if (pageIdMatch.Success)
                {
                    pageProgrammaticId = pageIdMatch.Groups[1].Value;
                }

                RegisterPageMapping(page.Id, page.OneNoteId, pageProgrammaticId, pagePath, page.Title);
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to generate programmatic ID for page {page.Title}: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert OneNote internal links to markdown references
        /// </summary>
        /// <param name="pageTxt">Markdown content</param>
        /// <returns>Updated markdown content with converted links</returns>
        public string ConvertOneNoteLinks(string pageTxt, Func<string, string, string, string> getWikilink, bool usePlaceholders = false)
        {
            if (AppSettings.OneNoteLinksHandling == OneNoteLinksHandlingEnum.KeepOriginal)
            {
                return pageTxt;
            }

            // Match markdown links with onenote:// URLs
            const string regexPattern = @"\[(?<text>[^\]]+)\]\(onenote:(?<url>[^\)]+)\)";

            return Regex.Replace(pageTxt, regexPattern, match =>
            {
                var linkText = match.Groups["text"].Value;
                var onenoteUrl = match.Groups["url"].Value;

                if (AppSettings.OneNoteLinksHandling == OneNoteLinksHandlingEnum.Remove)
                {
                    return linkText;
                }

                // With RepairLinks on, write a placeholder that LinkRepair resolves to a real relative path once
                // every page is on disk (so a link to a page exported later is not lost). Markdown export only.
                if (usePlaceholders)
                {
                    var ph = Regex.Match(onenoteUrl, @"page-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                    if (ph.Success)
                        return $"[{linkText}](onenote-page:{ph.Groups[1].Value.ToLowerInvariant()} \"OneNote page link\")";
                    // not a page link (section, Outlook item, …): keep the current behaviour below
                }

                // Try to replace OneNote Page link

                // Extract page-id from URL
                const string pageIdPattern = @"page-id=\{([^}]+)\}";
                var pageIdMatch = Regex.Match(onenoteUrl, pageIdPattern, RegexOptions.IgnoreCase);
                if (pageIdMatch.Success)
                {
                    var programmaticId = pageIdMatch.Groups[1].Value;
                    if (PageMetadata.TryGetValue(programmaticId, out var pageMetadata))
                    {
                        Log.Debug($"ConvertOneNoteLinks - Found page: {pageMetadata.MdFilePath}, pageId: {programmaticId}");

                        // Normalize path to use forward slashes
                        return getWikilink(linkText, pageMetadata.MdFilePath, pageMetadata.NodeId);
                    }
                    else
                    {
                        Log.Debug($"ConvertOneNoteLinks - No link found for pageId: {programmaticId}");
                    }
                }

                // Try to replace OneNote Section link

                Log.Debug($"ConvertOneNoteLinks - Link {linkText} removed : {onenoteUrl}");
                // Link to a section, section group, or any other onenote unsupported link => return link text only

                return linkText;


            });

        }
    }
}
