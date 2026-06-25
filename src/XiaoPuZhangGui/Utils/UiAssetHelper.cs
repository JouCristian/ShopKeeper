using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            assetName = NormalizeAssetName(assetName);
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
            assetName = NormalizeAssetName(assetName);
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

        public static Image GetIllustration(string assetName)
        {
            return GetIllustration(assetName, new Size(480, 200));
        }

        public static Image GetIllustration(string assetName, Size fallbackSize)
        {
            assetName = NormalizeAssetName(assetName);
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

        public static string GetCustomAssetPath(string assetName)
        {
            assetName = NormalizeAssetName(assetName);
            string existing = FindCustomAssetPath(assetName, "icons");
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            existing = FindCustomAssetPath(assetName, "illustrations");
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            string rootAsset = Path.Combine(UserAssetDirectory, ToAssetPath(assetName) + ".png");
            if (File.Exists(rootAsset))
            {
                return rootAsset;
            }

            return rootAsset;
        }

        public static void EnsureUserAssetDirectories()
        {
            AppPaths.EnsureDirectory(UserAssetDirectory);
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "icons"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "icons", "png"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations", "png"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations", "png", "dashboard"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations", "png", "empty"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations", "png", "headers"));
            AppPaths.EnsureDirectory(Path.Combine(UserAssetDirectory, "illustrations", "png", "report"));
        }

        public static void OpenUserAssetDirectory()
        {
            EnsureUserAssetDirectories();
            Process.Start(UserAssetDirectory);
        }

        public static void ReloadAssetCache()
        {
            lock (CacheLock)
            {
                // Existing controls may still reference old Image instances, so clear the cache
                // without disposing them. New pages and refreshed controls will load fresh files.
                ImageCache.Clear();
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

        private static string UserAssetDirectory
        {
            get { return Path.Combine(AppPaths.RuntimeRoot, "assets"); }
        }

        private static Image LoadIcon(string assetName, int size)
        {
            Image image = LoadFirstPng(assetName, "icons");
            if (image == null)
            {
                return CreateFallbackIcon(assetName, size);
            }

            return ResizeImage(image, new Size(size, size), true);
        }

        private static Image LoadIllustration(string assetName, Size fallbackSize)
        {
            Image image = LoadFirstPng(assetName, "illustrations");
            if (image == null)
            {
                return CreateFallbackIllustration(fallbackSize);
            }

            return ResizeImage(image, fallbackSize, false);
        }

        private static Image LoadFirstPng(string assetName, string assetType)
        {
            string[] names = ResolveCandidateNames(assetName);
            foreach (string name in names)
            {
                string customPath = FindCustomAssetPath(name, assetType);
                if (!string.IsNullOrEmpty(customPath))
                {
                    Image custom = LoadPng(customPath);
                    if (custom != null)
                    {
                        return custom;
                    }
                }
            }

            foreach (string name in names)
            {
                string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetRoot, assetType, "png", ToAssetPath(name) + ".png");
                Image image = LoadPng(defaultPath);
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        private static string FindCustomAssetPath(string assetName, string assetType)
        {
            string assetPath = ToAssetPath(assetName) + ".png";
            string path = Path.Combine(UserAssetDirectory, assetType, "png", assetPath);
            if (File.Exists(path))
            {
                return path;
            }

            path = Path.Combine(UserAssetDirectory, assetType, assetPath);
            if (File.Exists(path))
            {
                return path;
            }

            path = Path.Combine(UserAssetDirectory, assetPath);
            return File.Exists(path) ? path : null;
        }

        private static string[] ResolveCandidateNames(string assetName)
        {
            string alias = ResolveAlias(assetName);
            if (string.Equals(assetName, alias, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { assetName };
            }

            return new[] { assetName, alias };
        }

        private static string ResolveAlias(string assetName)
        {
            if (assetName.StartsWith("nav_", StringComparison.OrdinalIgnoreCase))
            {
                string key = assetName.Substring(4);
                if (key == "dashboard")
                {
                    return "home";
                }

                return key;
            }

            if (assetName.StartsWith("action_", StringComparison.OrdinalIgnoreCase))
            {
                string key = assetName.Substring(7);
                if (key == "export")
                {
                    return "export_excel";
                }

                if (key == "restore")
                {
                    return "backup";
                }

                return key;
            }

            if (assetName.StartsWith("empty_", StringComparison.OrdinalIgnoreCase))
            {
                if (assetName.Contains("sales"))
                {
                    return "empty_cart";
                }

                if (assetName.Contains("credit"))
                {
                    return "empty_credit";
                }

                return "empty_box";
            }

            if (assetName == "dashboard_hero" || assetName == "login_hero" || assetName == "first_run_hero" || assetName == "recovery_key_hero")
            {
                return "shop_hero";
            }

            return assetName;
        }

        private static string NormalizeAssetName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return "empty_box";
            }

            string normalized = assetName.Trim().Replace('\\', '/');
            if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            string[] rawSegments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> segments = new List<string>();
            foreach (string rawSegment in rawSegments)
            {
                string segment = Path.GetFileNameWithoutExtension(rawSegment.Trim());
                if (!string.IsNullOrWhiteSpace(segment) && segment != "." && segment != "..")
                {
                    segments.Add(segment);
                }
            }

            return segments.Count == 0 ? "empty_box" : string.Join("/", segments.ToArray());
        }

        private static string ToAssetPath(string assetName)
        {
            return assetName.Replace('/', Path.DirectorySeparatorChar);
        }

        private static Image LoadPng(string path)
        {
            try
            {
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

        private static Image ResizeImage(Image image, Size targetSize, bool forceExact)
        {
            if (image == null)
            {
                return null;
            }

            int width = targetSize.Width;
            int height = targetSize.Height;
            if (!forceExact)
            {
                double ratio = Math.Min((double)targetSize.Width / image.Width, (double)targetSize.Height / image.Height);
                ratio = Math.Min(1D, ratio);
                width = Math.Max(1, (int)Math.Round(image.Width * ratio));
                height = Math.Max(1, (int)Math.Round(image.Height * ratio));
            }

            if (image.Width == width && image.Height == height)
            {
                return image;
            }

            Bitmap scaled = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(image, new Rectangle(0, 0, width, height));
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

        private static Image CreateFallbackIcon(string assetName, int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(UiTheme.PrimaryBlue, Math.Max(2F, size / 12F)))
            using (SolidBrush brush = new SolidBrush(UiTheme.PrimaryBlue))
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
                else if (assetName.Contains("search"))
                {
                    DrawSearchIcon(graphics, pen, size);
                }
                else if (assetName.Contains("save"))
                {
                    DrawSaveIcon(graphics, pen, size);
                }
                else if (assetName.Contains("add"))
                {
                    DrawAddIcon(graphics, pen, size);
                }
                else if (assetName.Contains("refresh"))
                {
                    DrawRefreshIcon(graphics, pen, size);
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
            using (SolidBrush blue = new SolidBrush(UiTheme.PrimaryBlue))
            using (SolidBrush lightBlue = new SolidBrush(Color.FromArgb(142, 197, 255)))
            using (SolidBrush shelf = new SolidBrush(Color.FromArgb(235, 247, 240)))
            using (SolidBrush warm = new SolidBrush(Color.FromArgb(255, 244, 216)))
            using (Pen border = new Pen(UiTheme.CardBorder, 3F))
            using (Pen plus = new Pen(UiTheme.PrimaryBlue, 8F))
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

        private static void DrawSearchIcon(Graphics graphics, Pen pen, int size)
        {
            graphics.DrawEllipse(pen, size * 0.18F, size * 0.18F, size * 0.45F, size * 0.45F);
            graphics.DrawLine(pen, size * 0.56F, size * 0.56F, size * 0.82F, size * 0.82F);
        }

        private static void DrawSaveIcon(Graphics graphics, Pen pen, int size)
        {
            graphics.DrawRectangle(pen, size * 0.18F, size * 0.18F, size * 0.64F, size * 0.64F);
            graphics.DrawLine(pen, size * 0.3F, size * 0.18F, size * 0.3F, size * 0.42F);
            graphics.DrawLine(pen, size * 0.28F, size * 0.64F, size * 0.72F, size * 0.64F);
        }

        private static void DrawAddIcon(Graphics graphics, Pen pen, int size)
        {
            graphics.DrawLine(pen, size * 0.5F, size * 0.22F, size * 0.5F, size * 0.78F);
            graphics.DrawLine(pen, size * 0.22F, size * 0.5F, size * 0.78F, size * 0.5F);
        }

        private static void DrawRefreshIcon(Graphics graphics, Pen pen, int size)
        {
            graphics.DrawArc(pen, size * 0.2F, size * 0.2F, size * 0.58F, size * 0.58F, 35, 255);
            graphics.DrawLine(pen, size * 0.72F, size * 0.21F, size * 0.78F, size * 0.38F);
            graphics.DrawLine(pen, size * 0.72F, size * 0.21F, size * 0.55F, size * 0.25F);
        }
    }
}
