using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiAssetHelper
    {
        private const string AssetRoot = "Assets";
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, Image> ImageCache = new Dictionary<string, Image>();

        public static Image GetIcon(string assetName, int size)
        {
            string cacheKey = "icon:" + assetName + ":" + size;
            lock (CacheLock)
            {
                if (!ImageCache.ContainsKey(cacheKey))
                {
                    ImageCache[cacheKey] = LoadIcon(assetName, size);
                }

                return ImageCache[cacheKey];
            }
        }

        public static Image GetIcon(string assetName, int size, Color tintColor)
        {
            string cacheKey = "icon:" + assetName + ":" + size + ":" + tintColor.ToArgb();
            lock (CacheLock)
            {
                if (!ImageCache.ContainsKey(cacheKey))
                {
                    ImageCache[cacheKey] = TintIcon(GetIcon(assetName, size), tintColor);
                }

                return ImageCache[cacheKey];
            }
        }

        public static Image GetIllustration(string assetName, Size fallbackSize)
        {
            string cacheKey = "illustration:" + assetName + ":" + fallbackSize.Width + "x" + fallbackSize.Height;
            lock (CacheLock)
            {
                if (!ImageCache.ContainsKey(cacheKey))
                {
                    ImageCache[cacheKey] = LoadIllustration(assetName, fallbackSize);
                }

                return ImageCache[cacheKey];
            }
        }

        public static void ApplyIcon(Button button, string assetName, int size)
        {
            Image icon = GetIcon(assetName, size);
            if (icon == null)
            {
                return;
            }

            button.Image = icon;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        public static void ApplyIcon(Button button, string assetName, int size, Color tintColor)
        {
            Image icon = GetIcon(assetName, size, tintColor);
            if (icon == null)
            {
                return;
            }

            button.Image = icon;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        private static Image LoadIcon(string assetName, int size)
        {
            Image image = LoadPng(Path.Combine(AssetRoot, "icons", "png", assetName + ".png"));
            if (image == null)
            {
                return CreateFallbackIcon(assetName, size);
            }

            if (image.Width == size && image.Height == size)
            {
                return image;
            }

            Bitmap scaled = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, new Rectangle(0, 0, size, size));
            }

            image.Dispose();
            return scaled;
        }

        private static Image TintIcon(Image source, Color tintColor)
        {
            if (source == null)
            {
                return null;
            }

            Bitmap sourceBitmap = new Bitmap(source);
            Bitmap tinted = new Bitmap(sourceBitmap.Width, sourceBitmap.Height);
            for (int y = 0; y < sourceBitmap.Height; y++)
            {
                for (int x = 0; x < sourceBitmap.Width; x++)
                {
                    Color sourceColor = sourceBitmap.GetPixel(x, y);
                    if (sourceColor.A == 0)
                    {
                        tinted.SetPixel(x, y, Color.Transparent);
                    }
                    else
                    {
                        tinted.SetPixel(x, y, Color.FromArgb(sourceColor.A, tintColor));
                    }
                }
            }

            sourceBitmap.Dispose();
            return tinted;
        }

        private static Image LoadIllustration(string assetName, Size fallbackSize)
        {
            Image image = LoadPng(Path.Combine(AssetRoot, "illustrations", "png", assetName + ".png"));
            return image ?? CreateFallbackIllustration(fallbackSize);
        }

        private static Image LoadPng(string relativePath)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (!File.Exists(path))
                {
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image CreateFallbackIcon(string assetName, int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(Color.FromArgb(31, 111, 235), Math.Max(2F, size / 12F)))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(31, 111, 235)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (assetName.Contains("warning"))
                {
                    DrawWarningIcon(graphics, pen, brush, size);
                }
                else if (assetName.Contains("credit"))
                {
                    DrawCreditIcon(graphics, pen, size);
                }
                else if (assetName.Contains("sales") || assetName.Contains("cart"))
                {
                    DrawCartIcon(graphics, pen, brush, size);
                }
                else
                {
                    DrawBoxIcon(graphics, pen, size);
                }
            }

            return bitmap;
        }

        private static Image CreateFallbackIllustration(Size size)
        {
            int width = Math.Max(240, size.Width);
            int height = Math.Max(120, size.Height);
            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(246, 248, 250)))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(31, 111, 235)))
            using (SolidBrush lightBlue = new SolidBrush(Color.FromArgb(142, 197, 255)))
            using (SolidBrush shelf = new SolidBrush(Color.FromArgb(243, 248, 237)))
            using (SolidBrush warm = new SolidBrush(Color.FromArgb(255, 244, 216)))
            using (Pen border = new Pen(Color.FromArgb(221, 228, 236), 3F))
            using (Pen plus = new Pen(Color.FromArgb(31, 111, 235), 8F))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillRectangle(background, 0, 0, width, height);
                graphics.FillRectangle(white, width * 15 / 100, height * 39 / 100, width * 52 / 100, height * 41 / 100);
                graphics.DrawRectangle(border, width * 15 / 100, height * 39 / 100, width * 52 / 100, height * 41 / 100);
                graphics.FillPolygon(blue, new[]
                {
                    new Point(width * 19 / 100, height * 28 / 100),
                    new Point(width * 63 / 100, height * 28 / 100),
                    new Point(width * 68 / 100, height * 44 / 100),
                    new Point(width * 14 / 100, height * 44 / 100)
                });
                graphics.FillPolygon(lightBlue, new[]
                {
                    new Point(width * 19 / 100, height * 28 / 100),
                    new Point(width * 28 / 100, height * 28 / 100),
                    new Point(width * 26 / 100, height * 44 / 100),
                    new Point(width * 14 / 100, height * 44 / 100)
                });
                graphics.FillRectangle(lightBlue, width * 21 / 100, height * 52 / 100, width * 15 / 100, height * 28 / 100);
                graphics.FillRectangle(shelf, width * 42 / 100, height * 52 / 100, width * 18 / 100, height * 13 / 100);
                graphics.FillRectangle(warm, width * 42 / 100, height * 69 / 100, width * 18 / 100, height * 11 / 100);
                graphics.FillEllipse(lightBlue, width * 70 / 100, height * 47 / 100, width * 14 / 100, height * 34 / 100);
                graphics.DrawLine(plus, width * 73 / 100, height * 64 / 100, width * 81 / 100, height * 64 / 100);
                graphics.DrawLine(plus, width * 77 / 100, height * 54 / 100, width * 77 / 100, height * 74 / 100);
            }

            return bitmap;
        }

        private static void DrawBoxIcon(Graphics graphics, Pen pen, int size)
        {
            PointF[] points =
            {
                new PointF(size * 0.2F, size * 0.36F),
                new PointF(size * 0.5F, size * 0.2F),
                new PointF(size * 0.8F, size * 0.36F),
                new PointF(size * 0.8F, size * 0.68F),
                new PointF(size * 0.5F, size * 0.84F),
                new PointF(size * 0.2F, size * 0.68F),
                new PointF(size * 0.2F, size * 0.36F)
            };
            graphics.DrawLines(pen, points);
            graphics.DrawLine(pen, size * 0.2F, size * 0.36F, size * 0.5F, size * 0.52F);
            graphics.DrawLine(pen, size * 0.8F, size * 0.36F, size * 0.5F, size * 0.52F);
            graphics.DrawLine(pen, size * 0.5F, size * 0.52F, size * 0.5F, size * 0.84F);
        }

        private static void DrawCartIcon(Graphics graphics, Pen pen, Brush brush, int size)
        {
            graphics.DrawLine(pen, size * 0.18F, size * 0.25F, size * 0.26F, size * 0.25F);
            graphics.DrawLine(pen, size * 0.26F, size * 0.25F, size * 0.36F, size * 0.63F);
            graphics.DrawLine(pen, size * 0.33F, size * 0.36F, size * 0.82F, size * 0.36F);
            graphics.DrawLine(pen, size * 0.82F, size * 0.36F, size * 0.75F, size * 0.63F);
            graphics.DrawLine(pen, size * 0.36F, size * 0.63F, size * 0.75F, size * 0.63F);
            graphics.FillEllipse(brush, size * 0.36F, size * 0.75F, size * 0.11F, size * 0.11F);
            graphics.FillEllipse(brush, size * 0.68F, size * 0.75F, size * 0.11F, size * 0.11F);
        }

        private static void DrawCreditIcon(Graphics graphics, Pen pen, int size)
        {
            graphics.DrawRectangle(pen, size * 0.18F, size * 0.3F, size * 0.64F, size * 0.43F);
            graphics.DrawLine(pen, size * 0.18F, size * 0.43F, size * 0.82F, size * 0.43F);
            graphics.DrawLine(pen, size * 0.31F, size * 0.58F, size * 0.52F, size * 0.58F);
        }

        private static void DrawWarningIcon(Graphics graphics, Pen pen, Brush brush, int size)
        {
            PointF[] points =
            {
                new PointF(size * 0.5F, size * 0.17F),
                new PointF(size * 0.84F, size * 0.78F),
                new PointF(size * 0.16F, size * 0.78F),
                new PointF(size * 0.5F, size * 0.17F)
            };
            graphics.DrawLines(pen, points);
            graphics.DrawLine(pen, size * 0.5F, size * 0.38F, size * 0.5F, size * 0.58F);
            graphics.FillEllipse(brush, size * 0.46F, size * 0.67F, size * 0.08F, size * 0.08F);
        }
    }
}
