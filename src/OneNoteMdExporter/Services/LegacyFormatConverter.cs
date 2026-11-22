using Serilog;
using System;

namespace alxnbl.OneNoteMdExporter.Services
{
    /// <summary>
    /// Converts legacy image formats that most Markdown viewers cannot display. EMF / WMF metafiles are
    /// rendered to PNG with System.Drawing (Windows GDI+). The original metafile is never deleted; the
    /// PNG is written next to it and referenced instead.
    /// </summary>
    public static class LegacyFormatConverter
    {
        public static bool IsMetafile(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".emf" || ext == ".wmf";
        }

        /// <summary>
        /// Convert an EMF/WMF file to a PNG next to it (same name, .png). Returns the PNG path, or null on failure.
        /// </summary>
        public static string ConvertMetafileToPng(string metafilePath)
        {
            var pngPath = System.IO.Path.ChangeExtension(metafilePath, ".png");
            try
            {
                using var metafile = new System.Drawing.Imaging.Metafile(metafilePath);
                var header = metafile.GetMetafileHeader();

                // Render at the metafile's own size, capped so the PNG stays a reasonable size. One scale for
                // both sides, so a wide or tall drawing is not squashed.
                var scale = Math.Min(1.0, 4000.0 / Math.Max(1, Math.Max(metafile.Width, metafile.Height)));
                var width = Math.Max(1, (int)Math.Round(metafile.Width * scale));
                var height = Math.Max(1, (int)Math.Round(metafile.Height * scale));

                using var bitmap = new System.Drawing.Bitmap(width, height);
                bitmap.SetResolution(Math.Max(header.DpiX, 96), Math.Max(header.DpiY, 96));
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(metafile, 0, 0, width, height);
                }
                bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                return pngPath;
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not convert '{metafilePath}' to PNG: {ex.Message}");
                return null;
            }
        }
    }
}
