using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace alxnbl.OneNoteMdExporter.IntTests.Helpers
{
    public static class TestHelper
    {
        public static (string output, int exitCode) RunExporter(string args = "")
        {

            var startInfo = new ProcessStartInfo
            {
                FileName = "alxnbl.OneNoteMdExporter.exe",
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

                return (output: output, exitCode: exeProcess.ExitCode);
            }

        }
    }
}
