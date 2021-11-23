using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace alxnbl.OneNoteMdExporter.IntTests.Helpers
{
    public static class TestHelper
    {
        public static (string output, int exitCode, string exportResult) RunExporter(string format, string notebook, string section, string page)
        {
            string args = $"-f \"{format}\" -n \"{notebook}\" -s \"{section}\" -p \"{page}\" --no-input";

            var startInfo = new ProcessStartInfo
            {
                FileName = "OneNoteMdExporter.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string output = string.Empty;

            using (Process exeProcess = Process.Start(startInfo))
            {
                exeProcess.WaitForExit();

                if (exeProcess.ExitCode != 0)
                {
                    output = exeProcess.StandardError.ReadToEnd();
                }
                else
                {
                    output = exeProcess.StandardOutput.ReadToEnd();
                }

                string exportResult = format == "1" ? TestHelper.GetMdExportResult(notebook, section, page) : "";

                return (output, exeProcess.ExitCode, exportResult);
            }

        }

        public static string GetMdExportResult(string notebook, string section, string page)
        {
            return File.ReadAllText(Path.Combine(notebook, section, page + ".md"));
        }
    }
}
