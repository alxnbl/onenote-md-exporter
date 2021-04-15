using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        /// Convert DocX file into MD
        /// </summary>
        /// <param name="page">OneNote  page to convert</param>
        /// <param name="inputFilePath">docx file exported from OneNote page</param>
        /// <param name="resourceFolderPath">path to the folder container attachements</param>
        /// <param name="sectionTreeLevel">Level in the page hierarchy of one note, to indent the page title in Joplin</param>
        public string ConvertDocxToMd(Page page, string inputFilePath, string resourceFolderPath, int sectionTreeLevel)
        {
           
            var mdFilePath = Path.Combine("tmp", page.TitleWithNoInvalidChars + ".md");

            var arguments = $"\"{Path.GetFullPath(inputFilePath)}\"  " +
                            $"--to gfm " +
                            $"-o \"{Path.GetFullPath(mdFilePath)}\" " +
                            $"--wrap=none " + // Mandatory to avoid random quote bloc to be added to markdown
                            $"--extract-media=\"tmp\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "pandoc.exe",
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
                }
                else
                {
                    Log.Error($"{page.Id} : Pandoc error");
                    var pandocError = exeProcess.StandardError.ReadToEnd();
                    Log.Error("Pandoc error output: {0}", pandocError);
                }


                if (_appSettings.Debug)
                {
                    Log.Debug($"Pandoc output: {exeProcess.StandardOutput.ReadToEnd()}");
                }
            }

            var mdFileContent = File.ReadAllText(mdFilePath);

            if (!_appSettings.Debug)
                File.Delete(inputFilePath);

            return mdFileContent;
        }


        /// <summary>
        /// Apply post-convertion to md file
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent"></param>
        /// <param name="resourceFolderPath">The path to the notebook folder where store attachments</param>
        /// <param name="mdFilePath">Path where the md file will be exported</param>
        public string PostConvertion(Page page, string mdFileContent, string resourceFolderPath, string mdFilePath, bool joplinResourceRef)
        {
            var mdFileContentResult = mdFileContent;


            if (_appSettings.RemoveCarriageReturn)
            {
                mdFileContentResult = RemoveCarriageReturn(mdFileContentResult);
            }

            if (_appSettings.PostProcessingRemoveQuotationBlocks)
            {
                mdFileContentResult = RemoveQuotationBlocks(mdFileContentResult);
            }

            if (_appSettings.RemoveConsecutiveLinebreaks)
            {
                mdFileContentResult = RemoveConsecutiveLinebreaks(mdFileContentResult);
            }


            if (_appSettings.PostProcessingRemoveOneNoteHeader)
            {
                mdFileContentResult = RemoveOneNoteHeader(mdFileContentResult);
            }

            return mdFileContentResult;
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

        /// <summary>
        /// Replace PanDoc IMG HTML tag by markdown reference and copy image file into notebook export directory
        /// </summary>
        /// <param name="page">Section page</param>
        /// <param name="mdFileContent">Contennt of the MD file</param>
        /// <param name="resourceFolderPath">The path to the notebook folder where store attachments</param>
        /// <param name="sectionTreeLevel">Level in the page hierarchy of one note, to indent the page title in Joplin</param>
        /// <returns></returns>
        public string ExtractImagesToResourceFolder(Page page, string mdFileContent, string resourceFolderPath, string mdFilePath, bool joplinResourceRef, bool postProcessingMdImgRef)
        {
            // Search of <IMG> tags
            var pageTxtModified = mdFileContent;

            if (postProcessingMdImgRef)
            {
                pageTxtModified = Regex.Replace(mdFileContent, "<img [^>]+/>", delegate (Match match)
                {
                    // Process an <IMG> tag

                    string imageTag = match.ToString();

                    // http://regexstorm.net/tester
                    string regexImgAttributes = "<img src=\"(?<src>[^\"]+)\".* />";

                    MatchCollection matchs = Regex.Matches(imageTag, regexImgAttributes, RegexOptions.IgnoreCase);
                    Match imgMatch = matchs[0];

                    var padDocImgPath = imgMatch.Groups["src"].Value;

                    var imgAttach = page.ImageAttachements.Where(img => img.PanDocFilePath == padDocImgPath).FirstOrDefault();

                    if (imgAttach == null)
                    {
                        // Add a new attachmeent to current page
                        imgAttach = new Attachements(page)
                        {
                            Type = AttachementType.Image,
                            PanDocFilePath = padDocImgPath,
                        };

                        imgAttach.ExportFileName = imgAttach.Id + Path.GetExtension(padDocImgPath);
                        imgAttach.ExportFilePath = Path.Combine(resourceFolderPath, imgAttach.ExportFileName);

                        page.Attachements.Add(imgAttach);
                    }

                    var attachRef = joplinResourceRef ?
                        $":/{imgAttach.Id}" :
                        GetImgMdReference(Path.GetRelativePath(Path.GetDirectoryName(mdFilePath), resourceFolderPath), imgAttach.ExportFileName);

                    return $"![{imgAttach.Id}]({attachRef})";
                });
            }

            // Move attachements file into output ressource folder and delete tmp file
            foreach (var attach in page.ImageAttachements)
            {
                File.Copy(attach.PanDocFilePath, attach.ExportFilePath);
                File.Delete(attach.PanDocFilePath);
            }

            return pageTxtModified;
        }

        private static string GetImgMdReference(string relativePathFromMdFileToResourceFolder, string fileName)
        {
            return Path.Combine(relativePathFromMdFileToResourceFolder, fileName).Replace("\\", "/");
        }
    }

}
