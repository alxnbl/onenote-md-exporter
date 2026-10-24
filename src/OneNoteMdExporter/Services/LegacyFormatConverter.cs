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

        public static bool IsXps(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".xps" || ext == ".oxps";
        }

        /// <summary>
        /// Render each page of an XPS document (a OneNote printout attachment) to "&lt;name&gt;-p1.png",
        /// "&lt;name&gt;-p2.png"... Returns the PNG paths, empty on failure. Runs on a dedicated STA thread, as WPF
        /// requires. The original XPS file is never deleted.
        /// </summary>
        public static System.Collections.Generic.List<string> ConvertXpsToPngs(string xpsPath, int dpi)
        {
            var result = new System.Collections.Generic.List<string>();
            Exception error = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    using var xps = new System.Windows.Xps.Packaging.XpsDocument(xpsPath, System.IO.FileAccess.Read);
                    var paginator = xps.GetFixedDocumentSequence().DocumentPaginator;
                    paginator.ComputePageCount();

                    var stem = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(xpsPath), System.IO.Path.GetFileNameWithoutExtension(xpsPath));
                    var scale = dpi / 96.0;

                    for (int i = 0; i < paginator.PageCount; i++)
                    {
                        using var docPage = paginator.GetPage(i);
                        var size = docPage.Size;
                        var pixelWidth = Math.Max(1, (int)(size.Width * scale));
                        var pixelHeight = Math.Max(1, (int)(size.Height * scale));

                        // White background first, then the page content on top (PNG would otherwise be transparent)
                        var background = new System.Windows.Media.DrawingVisual();
                        using (var dc = background.RenderOpen())
                            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(size));

                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(background);
                        bitmap.Render(docPage.Visual);

                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

                        var pngPath = $"{stem}-p{i + 1}.png";
                        using (var fs = System.IO.File.Create(pngPath))
                            encoder.Save(fs);

                        result.Add(pngPath);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null)
            {
                Log.Warning($"Could not convert '{xpsPath}' to PNG: {error.Message}");
                result.Clear();
            }

            return result;
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
