using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services
{
    public static class ConverterService
    {
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
                            $"--to {AppSettings.PanDocMarkdownFormat} " +
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
    }
}
