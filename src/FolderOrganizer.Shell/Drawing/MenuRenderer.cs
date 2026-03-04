// src/FolderOrganizer.Shell/Drawing/MenuRenderer.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using FolderOrganizer.Core.Icons;
using FolderOrganizer.Core.Models;

namespace FolderOrganizer.Shell.Drawing
{
    internal static class MenuRenderer
    {
        // Layout constants
        public const int CIRCLE_SIZE     = 20;
        public const int CIRCLE_PADDING  = 6;
        public const int ROW_HEIGHT      = 36;
        public const int ROW_PADDING_LEFT = 28;

        public const int ICON_CELL_SIZE  = 32;
        public const int ICON_PADDING    = 4;
        public const int ICON_ROW_HEIGHT = 44;

        public const int TAG_ROW_HEIGHT  = 36;

        #region Color Row

        public static Size MeasureColorRow()
        {
            int count = ChromePalette.Colors.Count + 1; // +1 for "+" button
            int width = ROW_PADDING_LEFT + count * (CIRCLE_SIZE + CIRCLE_PADDING) + CIRCLE_PADDING + 60;
            return new Size(width, ROW_HEIGHT);
        }

        public static void DrawColorRow(IntPtr hdc, Rectangle bounds, string activeColor)
        {
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    // Background
                    using (var bgBrush = new SolidBrush(SystemColors.Menu))
                        g.FillRectangle(bgBrush, bounds);

                    // "Color" label
                    using (var font = new Font("Segoe UI", 9f))
                    using (var textBrush = new SolidBrush(SystemColors.MenuText))
                        g.DrawString("Color", font, textBrush,
                            bounds.Left + 6, bounds.Top + (ROW_HEIGHT - 14) / 2);

                    int x = bounds.Left + ROW_PADDING_LEFT;
                    int y = bounds.Top + (ROW_HEIGHT - CIRCLE_SIZE) / 2;

                    foreach (var entry in ChromePalette.Colors)
                    {
                        var color = ColorTranslator.FromHtml(entry.Hex);
                        var rect  = new Rectangle(x, y, CIRCLE_SIZE, CIRCLE_SIZE);

                        using (var brush = new SolidBrush(color))
                            g.FillEllipse(brush, rect);

                        // Checkmark if active
                        if (entry.Hex.Equals(activeColor, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var pen = new Pen(Color.White, 2f))
                            {
                                g.DrawLine(pen, x + 5, y + 10, x + 9,  y + 14);
                                g.DrawLine(pen, x + 9, y + 14, x + 15, y + 6);
                            }
                        }

                        x += CIRCLE_SIZE + CIRCLE_PADDING;
                    }

                    // "+" button
                    var plusRect = new Rectangle(x, y, CIRCLE_SIZE, CIRCLE_SIZE);
                    using (var plusBrush = new SolidBrush(Color.FromArgb(100, 128, 128, 128)))
                        g.FillEllipse(plusBrush, plusRect);
                    using (var plusPen = new Pen(Color.Gray, 1.5f))
                        g.DrawEllipse(plusPen, plusRect);
                    using (var plusFont = new Font("Segoe UI", 12f, FontStyle.Bold))
                    using (var whiteBrush = new SolidBrush(Color.White))
                        g.DrawString("+", plusFont, whiteBrush, x + CIRCLE_SIZE / 2 - 5, y + CIRCLE_SIZE / 2 - 8);
                }
            }
            catch { }
        }

        /// <summary>
        /// Returns 0..8 for Chrome palette circles, 9 for "+", -1 for miss.
        /// </summary>
        public static int HitTestColorCircle(Point cursor, Rectangle bounds)
        {
            int x = bounds.Left + ROW_PADDING_LEFT;
            int y = bounds.Top + (ROW_HEIGHT - CIRCLE_SIZE) / 2;

            for (int i = 0; i <= ChromePalette.Colors.Count; i++) // +1 for "+"
            {
                var center = new Point(x + CIRCLE_SIZE / 2, y + CIRCLE_SIZE / 2);
                double dist = Math.Sqrt(
                    Math.Pow(cursor.X - center.X, 2) + Math.Pow(cursor.Y - center.Y, 2));
                if (dist <= CIRCLE_SIZE / 2.0)
                    return i;
                x += CIRCLE_SIZE + CIRCLE_PADDING;
            }
            return -1;
        }

        #endregion

        #region Icon Row

        public static Size MeasureIconRow(int iconCount)
        {
            if (iconCount == 0) iconCount = 1; // show placeholder
            int width = ROW_PADDING_LEFT + iconCount * (ICON_CELL_SIZE + ICON_PADDING) + ICON_PADDING + 60;
            return new Size(width, ICON_ROW_HEIGHT);
        }

        public static void DrawIconRow(IntPtr hdc, Rectangle bounds, List<string> iconFiles)
        {
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    using (var bgBrush = new SolidBrush(SystemColors.Menu))
                        g.FillRectangle(bgBrush, bounds);

                    using (var font = new Font("Segoe UI", 9f))
                    using (var textBrush = new SolidBrush(SystemColors.MenuText))
                        g.DrawString("Icon", font, textBrush,
                            bounds.Left + 6, bounds.Top + (ICON_ROW_HEIGHT - 14) / 2);

                    int x = bounds.Left + ROW_PADDING_LEFT;
                    int y = bounds.Top + (ICON_ROW_HEIGHT - ICON_CELL_SIZE) / 2;

                    if (iconFiles == null || iconFiles.Count == 0)
                    {
                        // Placeholder when no icons exist
                        using (var pen = new Pen(Color.Gray))
                            g.DrawRectangle(pen, x, y, ICON_CELL_SIZE, ICON_CELL_SIZE);
                        using (var font = new Font("Segoe UI", 8f))
                        using (var brush = new SolidBrush(Color.Gray))
                            g.DrawString("No icons", font, brush, x, y + ICON_CELL_SIZE / 2 - 6);
                        return;
                    }

                    foreach (var iconFile in iconFiles)
                    {
                        try
                        {
                            var thumb = LoadPreviewThumbnail(iconFile, ICON_CELL_SIZE);
                            if (thumb != null)
                            {
                                g.DrawImage(thumb, x, y, ICON_CELL_SIZE, ICON_CELL_SIZE);
                                thumb.Dispose();
                            }
                            else
                            {
                                using (var pen = new Pen(Color.LightGray))
                                    g.DrawRectangle(pen, x, y, ICON_CELL_SIZE, ICON_CELL_SIZE);
                            }
                        }
                        catch { }

                        x += ICON_CELL_SIZE + ICON_PADDING;
                    }
                }
            }
            catch { }
        }

        /// <summary>Returns 0-based index of icon under cursor, or -1 for miss.</summary>
        public static int HitTestIconCell(Point cursor, Rectangle bounds, int iconCount)
        {
            int x = bounds.Left + ROW_PADDING_LEFT;
            int y = bounds.Top + (ICON_ROW_HEIGHT - ICON_CELL_SIZE) / 2;

            for (int i = 0; i < iconCount; i++)
            {
                var rect = new Rectangle(x, y, ICON_CELL_SIZE, ICON_CELL_SIZE);
                if (rect.Contains(cursor))
                    return i;
                x += ICON_CELL_SIZE + ICON_PADDING;
            }
            return -1;
        }

        private static Bitmap LoadPreviewThumbnail(string path, int size = 32)
        {
            try
            {
                using (var original = new Bitmap(path))
                {
                    var thumb = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, 0, 0, size, size);
                    }
                    return thumb;
                }
            }
            catch { return null; }
        }

        #endregion

        #region Tag Row

        public static Size MeasureTagRow(int tagCount)
        {
            // Estimate: each pill ~ 80px wide, min width 200
            int width = Math.Max(200, ROW_PADDING_LEFT + tagCount * 86 + 60);
            return new Size(width, TAG_ROW_HEIGHT);
        }

        public static void DrawTagRow(IntPtr hdc, Rectangle bounds,
            List<TagEntry> allTags, List<string> activeTags)
        {
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    using (var bgBrush = new SolidBrush(SystemColors.Menu))
                        g.FillRectangle(bgBrush, bounds);

                    using (var labelFont = new Font("Segoe UI", 9f))
                    using (var labelBrush = new SolidBrush(SystemColors.MenuText))
                        g.DrawString("Tags", labelFont, labelBrush,
                            bounds.Left + 6, bounds.Top + (TAG_ROW_HEIGHT - 14) / 2);

                    if (allTags == null || allTags.Count == 0)
                    {
                        using (var font = new Font("Segoe UI", 8f))
                        using (var brush = new SolidBrush(Color.Gray))
                            g.DrawString("No tags defined", font, brush,
                                bounds.Left + ROW_PADDING_LEFT, bounds.Top + (TAG_ROW_HEIGHT - 12) / 2);
                        return;
                    }

                    using (var font = new Font("Segoe UI", 8.5f))
                    {
                        int x = bounds.Left + ROW_PADDING_LEFT;
                        int y = bounds.Top + 8;

                        foreach (var tag in allTags)
                        {
                            var tagColor = ColorTranslator.FromHtml(tag.Color);
                            var textSize = g.MeasureString(tag.Name, font);
                            int pillWidth  = (int)textSize.Width + 12;
                            int pillHeight = 20;
                            var pillRect   = new RectangleF(x, y, pillWidth, pillHeight);

                            bool isActive = activeTags != null && activeTags.Contains(tag.Name);
                            var fillColor = isActive ? tagColor : Color.FromArgb(40, tagColor);

                            using (var fillBrush = new SolidBrush(fillColor))
                                FillRoundedRectangle(g, fillBrush, pillRect, 10f);

                            // Border for active tags
                            if (isActive)
                            {
                                using (var borderPen = new Pen(tagColor, 1.5f))
                                    DrawRoundedRectangle(g, borderPen, pillRect, 10f);
                            }

                            var textColor = isActive ? Color.White : tagColor;
                            using (var textBrush = new SolidBrush(textColor))
                                g.DrawString(tag.Name, font, textBrush, x + 6, y + 3);

                            x += pillWidth + 6;
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>Returns 0-based index of tag pill under cursor, or -1 for miss.</summary>
        public static int HitTestTagPill(Point cursor, Rectangle bounds,
            List<TagEntry> allTags, Graphics g)
        {
            if (allTags == null || allTags.Count == 0) return -1;

            int x = bounds.Left + ROW_PADDING_LEFT;
            int y = bounds.Top + 8;

            using (var font = new Font("Segoe UI", 8.5f))
            {
                for (int i = 0; i < allTags.Count; i++)
                {
                    var textSize = g.MeasureString(allTags[i].Name, font);
                    int pillWidth = (int)textSize.Width + 12;
                    var pillRect  = new Rectangle(x, y, pillWidth, 20);
                    if (pillRect.Contains(cursor))
                        return i;
                    x += pillWidth + 6;
                }
            }
            return -1;
        }

        #endregion

        #region GDI+ Rounded Rectangle Helpers

        private static void FillRoundedRectangle(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (var path = BuildRoundedPath(rect, radius))
                g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, RectangleF rect, float radius)
        {
            using (var path = BuildRoundedPath(rect, radius))
                g.DrawPath(pen, path);
        }

        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
        {
            float r2 = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X,                       rect.Y,                        r2, r2, 180, 90);
            path.AddArc(rect.Right - r2,              rect.Y,                        r2, r2, 270, 90);
            path.AddArc(rect.Right - r2,              rect.Bottom - r2,              r2, r2,   0, 90);
            path.AddArc(rect.X,                       rect.Bottom - r2,              r2, r2,  90, 90);
            path.CloseAllFigures();
            return path;
        }

        #endregion
    }
}
