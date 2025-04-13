using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace alxnbl.OneNoteMdExporter.Services
{
    public static class ConverterService
    {
        // Dictionary to store page and section mappings
        private static readonly Dictionary<string, OneNoteLinkMetadata> PageMetadata = new();
        private static readonly Dictionary<string, OneNoteLinkMetadata> SectionMetadata = new();

        /// <summary>
        /// Register a page mapping for link conversion
        /// </summary>
        /// <param name="pageId">OneNote page ID</param>
        /// <param name="programmaticId">Programmatic ID generated from OneNote</param>
        /// <param name="exportPath">Relative export path of the page</param>
        /// <param name="title">Page title</param>
        public static void RegisterPageMapping(string pageId, string programmaticId, string exportPath, string title)
        {
            PageMetadata[pageId] = new OneNoteLinkMetadata
            {
                OriginalId = pageId,
                ProgrammaticId = programmaticId,
                MdFilePath = exportPath,
                Title = title
            };
        }

        /// <summary>
        /// Register a section mapping for link conversion
        /// </summary>
        /// <param name="sectionId">OneNote section ID</param>
        /// <param name="programmaticId">Programmatic ID generated from OneNote</param>
        /// <param name="exportPath">Relative export path of the section</param>
        /// <param name="title">Section title</param>
        public static void RegisterSectionMapping(string sectionId, string programmaticId, string exportPath, string title)
        {
            SectionMetadata[sectionId] = new OneNoteLinkMetadata
            {
                OriginalId = sectionId,
                ProgrammaticId = programmaticId,
                MdFilePath = exportPath,
                Title = title
            };
        }

        /// <summary>
        /// Convert DocX file into MD using PanDoc
        /// </summary>
        /// <param name="page">OneNote  page to convert</param>
        /// <param name="inputFilePath">docx file exported from OneNote page</param>
        /// <param name="tmpFolderPath">path to the folder container attachments</param>
        public static string ConvertDocxToMd(Page page, string inputFilePath, string tmpFolderPath)
        {
            var tmpDir = Path.Combine(tmpFolderPath, "pandoc");
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
            Directory.CreateDirectory(tmpDir);

            var pandocPath = @"pandoc\pandoc.exe";
            if (!File.Exists(pandocPath))
                pandocPath = "pandoc.exe";

            var mdFilePath = Path.Combine(tmpDir, page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");

            var arguments = $"\"{Path.GetFullPath(inputFilePath)}\" " +
                            $"--to={AppSettings.PanDocMarkdownFormat} " +
                            $"-o \"{Path.GetFullPath(mdFilePath)}\" " +
                            $"--wrap=none " + // Mandatory to avoid random quote block to be added to markdown
                            $"--extract-media=\"{tmpDir}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = pandocPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Log.Debug($"{page.Id} : Start Pandoc");

            try
            {
                using Process exeProcess = Process.Start(startInfo);
                exeProcess.WaitForExit();

                if (exeProcess.ExitCode == 0)
                {
                    Log.Debug($"{page.Id} : Pandoc success");


                    if (AppSettings.Debug)
                        Log.Debug($"Pandoc output: {exeProcess.StandardOutput.ReadToEnd()}");

                    File.Delete(inputFilePath);

                    var mdFileContent = File.ReadAllText(mdFilePath);

                    return mdFileContent;
                }
                else
                {
                    Log.Error($"{page.Id} : Pandoc error");
                    var pandocError = exeProcess.StandardError.ReadToEnd();
                    Log.Error("Pandoc error output: {0}", pandocError);

                    throw new Exception("Error during PanDoc execution");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new Exception(@"pandoc.exe not found in \pandoc\ subfolder. Have you unzip the pandoc archive ?");
            }
        }

        /// <summary>
        /// Apply post-conversion to md file
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent"></param>
        public static void PageMdPostConversion(ref string mdFileContent)
        {
            if (AppSettings.DeduplicateLinebreaks)
            {
                mdFileContent = DeduplicateLinebreaks(mdFileContent);
            }

            mdFileContent = RemoveHtmlCommentBlocks(mdFileContent);

            mdFileContent = RemoveUTF8NonBreakingSpace(mdFileContent);

            if (AppSettings.PostProcessingRemoveQuotationBlocks)
            {
                mdFileContent = RemoveQuotationBlocks(mdFileContent);
            }

            if (AppSettings.MaxTwoLineBreaksInARow)
            {
                mdFileContent = MaxTwoLineBreaksInARow(mdFileContent);
            }

            if (AppSettings.PostProcessingRemoveOneNoteHeader)
            {
                mdFileContent = RemoveOneNoteHeader(mdFileContent);
            }

            mdFileContent = InsertMdHighlight(mdFileContent);
            
            mdFileContent = ConvertOneNoteLinks(mdFileContent);
        }

        private static string RemoveOneNoteHeader(string pageTxt)
        {
            var pageTxtModified = Regex.Replace(pageTxt, @"^.+(\n|\r|\r\n){1,2}.+(\n|\r|\r\n){1,2}\d{2}:\d{2}\s+", "");

            return pageTxtModified;
        }

        private static string RemoveUTF8NonBreakingSpace(string pageTxt)
        {
            // Max 2 consecutive linebreaks
            var pageTxtModified = Regex.Replace(pageTxt, @"(\xa0|\xc2|\xc2\xa0)", string.Empty);

            return pageTxtModified;
        }

        private static string RemoveHtmlCommentBlocks(string pageTxt)
        {
            // Pandoc produce <!-- --> tags
            var pageTxtModified = Regex.Replace(pageTxt, @"(\n|\r|\r\n)( )*\<!--( )*--\>( )*", "");

            return pageTxtModified;
        }

        

        private static string DeduplicateLinebreaks(string pageTxt)
        {
            // PanDoc seems to produce 2 linebreaks characters for each linebreak in original DocX file
            // Replace all pair of linebreak by a single linebreak
            var pageTxtModified = Regex.Replace(pageTxt, @"(\n{2}|\r{2}|(\r\n){2})", Environment.NewLine);

            return pageTxtModified;
        }

        private static string MaxTwoLineBreaksInARow(string pageTxt)
        {
            // Max 2 consecutive linebreaks
            var pageTxtModified = Regex.Replace(pageTxt, @"((\n[ \t]*\n+)|(\r[ \t]*\r+)|(\r\n[ \t]*(\r\n)+))",
                Environment.NewLine + Environment.NewLine, RegexOptions.Multiline);

            return pageTxtModified;
        }

        private static string RemoveQuotationBlocks(string pageTxt)
        {
            string regex = @"(\n|\r|\r\n)>(\n|\r|\r\n)";
            var pageTxtModified = Regex.Replace(pageTxt, regex, delegate (Match match)
            {
                return Environment.NewLine;
            });

            string regex2 = @"(\n|\r|\r\n)>[ ]?";
            pageTxtModified = Regex.Replace(pageTxtModified, regex2, delegate (Match match)
            {
                return Environment.NewLine;
            });


            return pageTxtModified;
        }

        /// <summary>
        /// Replace PanDoc html tags <span class="mark">text</span> by ==text== 
        /// </summary>
        /// <param name="pageTxt"></param>
        /// <returns></returns>
        private static string InsertMdHighlight(string pageTxt)
        {
            // match and replace each span block of a row
            string regex = @"\<span class=\""mark\""\>(?<text>((?!\</span\>).)*)\</span\>"; // https://stackoverflow.com/questions/406230/regular-expression-to-match-a-line-that-doesnt-contain-a-word
            var pageTxtModified = Regex.Replace(pageTxt, regex, delegate (Match match)
            {
                return "==" + (match.Groups["text"]?.Value ?? "") + "==";
            });

            return pageTxtModified;
        }

        /// <summary>
        /// Convert OneNote internal links to markdown references
        /// </summary>
        /// <param name="pageTxt">Markdown content</param>
        /// <returns>Updated markdown content with converted links</returns>
        private static string ConvertOneNoteLinks(string pageTxt)
        {
            // Match markdown links with onenote:// URLs
            var regex = @"\[(?<text>[^\]]+)\]\(onenote:(?<url>[^\)]+)\)";
            var pageTxtModified = Regex.Replace(pageTxt, regex, delegate (Match match)
            {
                var linkText = match.Groups["text"].Value;
                var onenoteUrl = match.Groups["url"].Value;
                
                Log.Debug($"Processing OneNote link: {onenoteUrl}");

                switch (AppSettings.OneNoteLinksHandling)
                {
                    case OneNoteLinksHandlingEnum.KeepOriginal:
                        return match.Value;
                    
                    case OneNoteLinksHandlingEnum.Remove:
                        return linkText;
                    
                    case OneNoteLinksHandlingEnum.ConvertToMarkdown:
                        // Extract page-id from URL
                        var pageIdMatch = Regex.Match(onenoteUrl, @"page-id=\{([^}]+)\}", RegexOptions.IgnoreCase);
                        
                        if (pageIdMatch.Success)
                        {
                            var pageId = pageIdMatch.Groups[1].Value;
                            Log.Debug($"Found page ID in link: {pageId}");
                            
                            // Try to find the page by both original ID and programmatic ID
                            OneNoteLinkMetadata pageMetadata = null;
                            
                            foreach (var entry in PageMetadata)
                            {
                                if (string.Equals(entry.Key, pageId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(entry.Value.ProgrammaticId, pageId, StringComparison.OrdinalIgnoreCase))
                                {
                                    pageMetadata = entry.Value;
                                    Log.Debug($"Found matching page path: {pageMetadata.MdFilePath}");
                                    break;
                                }
                            }
                            
                            if (pageMetadata != null)
                            {
                                return $"[{linkText}]({pageMetadata.MdFilePath}.md)";
                            }
                            
                            // Log the IDs and available keys for debugging
                            Log.Debug($"Unable to resolve OneNote link. Page ID: {pageId}");
                            Log.Debug($"Available page IDs: {string.Join(", ", PageMetadata.Keys)}");
                        }
                        return match.Value;
                    
                    default:
                        return match.Value;
                }
            });

            return pageTxtModified;
        }
    }
}
