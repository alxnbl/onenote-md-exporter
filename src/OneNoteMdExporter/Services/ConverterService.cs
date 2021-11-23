using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services
{
    public class ConverterService
    {
        private readonly AppSettings _appSettings;

        public ConverterService(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        /// <summary>
        /// Convert DocX file into MD using PanDoc
        /// </summary>
        /// <param name="page">OneNote  page to convert</param>
        /// <param name="inputFilePath">docx file exported from OneNote page</param>
        /// <param name="tmpFolderPath">path to the folder container attachements</param>
        public string ConvertDocxToMd(Page page, string inputFilePath, string tmpFolderPath)
        {
            var tmpDir = Path.Combine(tmpFolderPath, "pandoc");
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
            Directory.CreateDirectory(tmpDir);

            var pandocPath = "pandoc\\pandoc.exe";

            if (!File.Exists(pandocPath))
                throw new Exception("pandoc.exe not found in /pandoc/ subfolder. Have you unzip the pandoc archive ?");

            var mdFilePath = Path.Combine(tmpDir, page.TitleWithNoInvalidChars + ".md");

            var arguments = $"\"{Path.GetFullPath(inputFilePath)}\"  " +
                            $"--to gfm " +
                            $"-o \"{Path.GetFullPath(mdFilePath)}\" " +
                            $"--wrap=none " + // Mandatory to avoid random quote bloc to be added to markdown
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

            using (Process exeProcess = Process.Start(startInfo))
            {
                exeProcess.WaitForExit();

                if(exeProcess.ExitCode == 0)
                {
                    Log.Debug($"{page.Id} : Pandoc success");


                    if (_appSettings.Debug)
                        Log.Debug($"Pandoc output: {exeProcess.StandardOutput.ReadToEnd()}");
                    

                    var mdFileContent = File.ReadAllText(mdFilePath);

                    if (!_appSettings.Debug)
                        File.Delete(inputFilePath);

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
        }


        /// <summary>
        /// Apply post-convertion to md file
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent"></param>
        public void PageMdPostConvertion(Page page, ref string mdFileContent)
        {
            if (_appSettings.RemoveCarriageReturn)
            {
                mdFileContent = RemoveCarriageReturn(mdFileContent);
            }

            if (_appSettings.PostProcessingRemoveQuotationBlocks)
            {
                mdFileContent = RemoveQuotationBlocks(mdFileContent);
            }

            if (_appSettings.RemoveConsecutiveLinebreaks)
            {
                mdFileContent = RemoveConsecutiveLinebreaks(mdFileContent);
            }

            if (_appSettings.PostProcessingRemoveOneNoteHeader)
            {
                mdFileContent = RemoveOneNoteHeader(mdFileContent);
            }
        }

        private string RemoveCarriageReturn(string pageTxt)
        {
            // Issue #27
            // https://stackoverflow.com/questions/2282181/net-regex-dot-character-matches-carriage-return
            // DotNet regex have weird behavior with \r
            // Replacing all \r\n into \n to avoid issues with downstream regex

            string regex = @"\r\n";
            var pageTxtModified = Regex.Replace(pageTxt, regex, delegate (Match match)
            {
                return "\n";
            });

            return pageTxtModified;
        }

        private string RemoveOneNoteHeader(string pageTxt)
        {
            var pageTxtModified = pageTxt.Replace("\r", "");

            pageTxtModified = Regex.Replace(pageTxtModified, @"^.+\n\n.+\n\n\d{2}:\d{2}\s+", delegate (Match match)
            {
                return "";
            });

            return pageTxtModified;
        }

        private string RemoveConsecutiveLinebreaks(string pageTxt)
        {
            // Max 2 consecutive linebreaks
            var pageTxtModified = Regex.Replace(pageTxt, @"(\n[\t ]+){3,10}", delegate (Match match)
            {
                return "\n\n";
            });

            return pageTxtModified;
        }

        private string RemoveQuotationBlocks(string pageTxt)
        {
            string regex = @"\n>\n";
            var pageTxtModified = Regex.Replace(pageTxt, regex, delegate (Match match)
            {
                return "\n";
            });

            string regex2 = @"\n>[ ]?";
            pageTxtModified = Regex.Replace(pageTxtModified, regex2, delegate (Match match)
            {
                return "\n";
            });


            return pageTxtModified;
        }
    }

}
