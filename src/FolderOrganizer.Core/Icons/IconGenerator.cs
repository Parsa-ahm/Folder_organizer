// src/FolderOrganizer.Core/Icons/IconGenerator.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FolderOrganizer.Core.Icons;

public static class IconGenerator
{
    /// <summary>
    /// Extracts the system folder icon from shell32.dll.
    /// Returns a 256x256 bitmap, or null if extraction fails.
    /// </summary>
    public static Bitmap? GetSystemFolderIcon()
    {
        try
        {
            // Use SHGetFileInfo to get the folder icon handle
            var shfi = new SHFILEINFO();
            var result = SHGetFileInfo(
                "C:\\",
                FILE_ATTRIBUTE_DIRECTORY,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            using var icon = Icon.FromHandle(shfi.hIcon);
            var bmp = icon.ToBitmap();
            DestroyIcon(shfi.hIcon);

            // Scale to 256x256
            var scaled = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, 0, 0, 256, 256);
            return scaled;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Applies a color tint to a bitmap while preserving luminance.
    /// intensity: 0 = no tint, 1 = full color replacement.
    /// </summary>
    public static Bitmap TintBitmap(Bitmap source, Color tintColor, float intensity = 0.6f)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int bytes = Math.Abs(srcData.Stride) * source.Height;
        var buffer = new byte[bytes];
        Marshal.Copy(srcData.Scan0, buffer, 0, bytes);

        float tr = tintColor.R / 255f;
        float tg = tintColor.G / 255f;
        float tb = tintColor.B / 255f;

        for (int i = 0; i < bytes; i += 4)
        {
            float b = buffer[i] / 255f;
            float g = buffer[i + 1] / 255f;
            float r = buffer[i + 2] / 255f;

            // Luminance of original pixel
            float lum = 0.299f * r + 0.587f * g + 0.114f * b;

            // Blend: original color lerped toward tint * luminance
            float nr = Lerp(r, tr * lum * 2.0f, intensity);
            float ng = Lerp(g, tg * lum * 2.0f, intensity);
            float nb = Lerp(b, tb * lum * 2.0f, intensity);

            buffer[i]     = (byte)(Clamp(nb) * 255);
            buffer[i + 1] = (byte)(Clamp(ng) * 255);
            buffer[i + 2] = (byte)(Clamp(nr) * 255);
            buffer[i + 3] = buffer[i + 3]; // preserve alpha
        }

        Marshal.Copy(buffer, 0, dstData.Scan0, bytes);
        source.UnlockBits(srcData);
        result.UnlockBits(dstData);

        return result;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Clamp(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    // P/Invoke
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttribs,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
