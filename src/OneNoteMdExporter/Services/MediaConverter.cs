using alxnbl.OneNoteMdExporter.Infrastructure;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Services
{
    /// <summary>
    /// Readable copies of audio/video attachments, made with an external ffmpeg: WMA → MP3, WMV → MP4, and an
    /// optional machine transcription through a configurable command line. All of it is skipped when ffmpeg is
    /// not found, so the export never fails because a tool is missing; the original media file is always kept.
    /// </summary>
    public static class MediaConverter
    {
        private static bool? ffmpegAvailable;

        public static bool FfmpegAvailable
        {
            get
            {
                if (ffmpegAvailable.HasValue) return ffmpegAvailable.Value;
                if (string.IsNullOrWhiteSpace(AppSettings.FfmpegPath)) return (ffmpegAvailable = false).Value;
                var (code, _, err) = RunProcess(AppSettings.FfmpegPath, "-version", 20);
                if (code != 0 && !AppSettings.FfmpegPath.Contains(Path.DirectorySeparatorChar) && !AppSettings.FfmpegPath.Contains('/'))
                {
                    // A shell opened before "winget install ffmpeg" has no ffmpeg on its PATH. Look it up the way a
                    // new shell would (registry PATH, user and machine) and in winget's links folder.
                    var found = FindOnRegisteredPath(AppSettings.FfmpegPath);
                    if (found != null)
                    {
                        Log.Information($"ffmpeg found at '{found}' (not on this process's PATH)");
                        AppSettings.FfmpegPath = found;
                        (code, _, err) = RunProcess(found, "-version", 20);
                    }
                }
                ffmpegAvailable = code == 0;
                if (!ffmpegAvailable.Value)
                    Log.Warning($"ffmpeg not found at '{AppSettings.FfmpegPath}' ({err}). MP3/MP4 copies and transcription are skipped.");
                return ffmpegAvailable.Value;
            }
        }

        /// <summary>
        /// Find an executable the way a freshly opened shell would: the PATH stored in the registry (user, then
        /// machine) and winget's links folder. Returns the full path, or null.
        /// </summary>
        private static string FindOnRegisteredPath(string exeName)
        {
            var name = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exeName : exeName + ".exe";
            var dirs = new List<string>();
            foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            {
                try { dirs.AddRange((Environment.GetEnvironmentVariable("PATH", scope) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)); }
                catch { }
            }
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links"));
            foreach (var dir in dirs)
            {
                try
                {
                    var candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir.Trim()), name);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        public static bool IsAudioToConvert(string path)
            => (AppSettings.AudioToMp3Extensions ?? Array.Empty<string>()).Any(e => string.Equals(e, Path.GetExtension(path), StringComparison.OrdinalIgnoreCase));

        public static bool IsVideoToConvert(string path)
            => (AppSettings.VideoToMp4Extensions ?? Array.Empty<string>()).Any(e => string.Equals(e, Path.GetExtension(path), StringComparison.OrdinalIgnoreCase));

        public static string ToMp3(string input, List<string> warnings)
        {
            var output = Path.ChangeExtension(input, ".mp3");
            return Ffmpeg($"-y -hide_banner -loglevel error -i \"{input}\" -vn -c:a libmp3lame -q:a 4 \"{output}\"", output, input, warnings);
        }

        public static string ToMp4(string input, List<string> warnings)
        {
            var output = Path.ChangeExtension(input, ".mp4");
            return Ffmpeg($"-y -hide_banner -loglevel error -i \"{input}\" -c:v libx264 -preset veryfast -crf 23 -c:a aac -b:a 128k -movflags +faststart \"{output}\"", output, input, warnings);
        }

        private static string Ffmpeg(string args, string output, string input, List<string> warnings)
        {
            if (!FfmpegAvailable) return null;
            var (code, _, err) = RunProcess(AppSettings.FfmpegPath, args, Math.Max(10, AppSettings.ConversionTimeoutSeconds) * 5);
            if (code == 0 && File.Exists(output) && new FileInfo(output).Length > 0)
                return output;
            warnings.Add($"ffmpeg failed on '{Path.GetFileName(input)}': {Trim(err)}");
            return null;
        }

        /// <summary>
        /// Transcribe with the configured command (TranscriptionCommand, with {input}/{output} placeholders).
        /// The media is first decoded to 16 kHz mono WAV with ffmpeg. Returns the transcript text, or null.
        /// </summary>
        public static string Transcribe(string mediaPath, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(AppSettings.TranscriptionCommand) || !FfmpegAvailable)
                return null;

            var wav = Path.Combine(Path.GetTempPath(), "onenote-transcribe-" + Guid.NewGuid().ToString("N") + ".wav");
            var outStem = Path.ChangeExtension(mediaPath, null) + ".transcript";
            try
            {
                var (code, _, err) = RunProcess(AppSettings.FfmpegPath, $"-y -hide_banner -loglevel error -i \"{mediaPath}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le \"{wav}\"", Math.Max(10, AppSettings.ConversionTimeoutSeconds) * 5);
                if (code != 0) { warnings.Add($"audio preparation for transcription failed: {Trim(err)}"); return null; }

                var cmd = AppSettings.TranscriptionCommand.Replace("{input}", wav).Replace("{output}", outStem);
                SplitCommand(cmd, out var exe, out var args);
                (code, _, err) = RunProcess(exe, args, 6 * 3600);
                var txt = outStem + ".txt";
                if (code == 0 && File.Exists(txt))
                    return File.ReadAllText(txt).Trim();

                warnings.Add($"transcription failed on '{Path.GetFileName(mediaPath)}': {Trim(err)}");
                return null;
            }
            finally
            {
                try { File.Delete(wav); } catch { }
            }
        }

        private static void SplitCommand(string cmd, out string exe, out string args)
        {
            cmd = cmd.Trim();
            if (cmd.StartsWith('"'))
            {
                var end = cmd.IndexOf('"', 1);
                exe = cmd[1..end];
                args = cmd[(end + 1)..].Trim();
            }
            else
            {
                var sp = cmd.IndexOf(' ');
                exe = sp < 0 ? cmd : cmd[..sp];
                args = sp < 0 ? "" : cmd[(sp + 1)..];
            }
        }

        private static (int code, string stdout, string stderr) RunProcess(string exe, string args, int timeoutSeconds)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutSeconds * 1000))
                {
                    try { p.Kill(true); } catch { }
                    return (-1, "", "timeout");
                }
                Task.WaitAll(outTask, errTask);
                return (p.ExitCode, outTask.Result, errTask.Result);
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }

        private static string Trim(string s)
        {
            s = (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            return s.Length > 200 ? s[..200] + "..." : s;
        }
    }
}
