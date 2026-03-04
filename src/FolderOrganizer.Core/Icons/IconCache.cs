// src/FolderOrganizer.Core/Icons/IconCache.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FolderOrganizer.Core.Icons;

/// <summary>
/// Manages pre-generated and cached tinted folder icons in AppData.
/// </summary>
public class IconCache
{
    private readonly string _cacheDir;

    public IconCache(string appDataPath)
    {
        _cacheDir = Path.Combine(appDataPath, "icon-cache");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>Returns path to a cached tinted icon for the given hex color. Generates if missing.</summary>
    public string? GetOrGenerateIcon(string hexColor)
    {
        try
        {
            var safeHex = hexColor.TrimStart('#');
            var iconPath = Path.Combine(_cacheDir, $"folder_{safeHex}.ico");

            if (File.Exists(iconPath))
                return iconPath;

            var baseBitmap = IconGenerator.GetSystemFolderIcon();
            if (baseBitmap is null) return null;

            var color = System.Drawing.ColorTranslator.FromHtml(hexColor);
            using var tinted = IconGenerator.TintBitmap(baseBitmap, color, 0.65f);

            SaveAsIco(tinted, iconPath);
            baseBitmap.Dispose();
            return iconPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Pre-generates all 9 Chrome palette icons. Call at app startup.</summary>
    public void EnsureChromePaletteGenerated()
    {
        foreach (var (_, hex) in ChromePalette.Colors)
        {
            try { GetOrGenerateIcon(hex); } catch { }
        }
    }

    private static void SaveAsIco(Bitmap bitmap, string path)
    {
        // Save as PNG (Explorer accepts .ico files that contain PNG data on Windows 7+)
        using var resized = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(resized);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(bitmap, 0, 0, 256, 256);

        // Write as ICO with PNG frame
        using var fs = new FileStream(path, FileMode.Create);
        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Png);
        var pngBytes = ms.ToArray();

        // ICO header
        fs.Write(new byte[] { 0, 0, 1, 0, 1, 0 }, 0, 6); // reserved, type=ICO, count=1
        // ICONDIRENTRY
        fs.Write(new byte[] { 0, 0, 0, 0, 1, 0, 32, 0 }, 0, 8); // 256x256, 1 plane, 32bpp
        var offset = 6 + 16; // header + one entry
        var sizeBytes = BitConverter.GetBytes(pngBytes.Length);
        var offsetBytes = BitConverter.GetBytes(offset);
        fs.Write(sizeBytes, 0, 4);
        fs.Write(offsetBytes, 0, 4);
        fs.Write(pngBytes, 0, pngBytes.Length);
    }
}
