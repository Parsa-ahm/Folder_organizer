// src/FolderOrganizer.Core/AppDataPaths.cs
using System;
using System.IO;

namespace FolderOrganizer.Core;

public static class AppDataPaths
{
    private static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderOrganizer");

    public static string RootDir => EnsureExists(Root);
    public static string IconCacheDir => EnsureExists(Path.Combine(Root, "icon-cache"));
    public static string CustomIconsDir => EnsureExists(Path.Combine(Root, "custom-icons"));
    public static string TagsFile => Path.Combine(Root, "tags.json");
    public static string TouchLogFile => Path.Combine(Root, "touched.log");
    public static string PresetsFile => Path.Combine(Root, "presets.json");
    public static string ThemeFile => Path.Combine(Root, "active-theme.json");

    private static string EnsureExists(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
