// src/FolderOrganizer.Core/Themes/VsixThemeImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderOrganizer.Core.Themes
{
    /// <summary>
    /// Imports a VS Code icon theme from a .vsix or .zip archive.
    /// Finds icon-theme.json inside the archive, extracts the theme,
    /// and returns a parsed <see cref="ThemeManifest"/>.
    /// </summary>
    public class VsixThemeImporter
    {
        /// <summary>
        /// Opens <paramref name="vsixOrZipPath"/>, locates <c>icon-theme.json</c>,
        /// extracts the entire archive to <paramref name="extractTo"/>,
        /// and returns the deserialized manifest.
        /// </summary>
        /// <param name="vsixOrZipPath">Path to the .vsix or .zip file.</param>
        /// <param name="extractTo">Target directory for extraction.</param>
        /// <returns>A <see cref="ThemeManifest"/> with icon mapping metadata.</returns>
        public ThemeManifest Import(string vsixOrZipPath, string extractTo)
        {
            if (!File.Exists(vsixOrZipPath))
                throw new FileNotFoundException("Theme archive not found.", vsixOrZipPath);

            using (var zip = ZipFile.OpenRead(vsixOrZipPath))
            {
                // Locate the icon-theme.json entry (may be nested in extension/ subdirectory)
                ZipArchiveEntry? manifestEntry = null;
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name.EndsWith("icon-theme.json", StringComparison.OrdinalIgnoreCase))
                    {
                        manifestEntry = entry;
                        break;
                    }
                }

                if (manifestEntry is null)
                    throw new InvalidOperationException(
                        "No icon-theme.json found in the archive. " +
                        "Make sure this is a VS Code icon theme extension.");

                // Extract everything to the target directory
                // (ExtractToDirectory with overwrite requires .NET Core 2.0+ so we do it manually)
                Directory.CreateDirectory(extractTo);
                foreach (var entry in zip.Entries)
                {
                    var destPath = Path.Combine(extractTo,
                        entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }
                    var dir = Path.GetDirectoryName(destPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                // Parse the manifest from the (now extracted) file, to avoid stream disposal issues
                var extractedManifestPath = Path.Combine(extractTo,
                    manifestEntry.FullName.Replace('/', Path.DirectorySeparatorChar));

                string json;
                if (File.Exists(extractedManifestPath))
                {
                    json = File.ReadAllText(extractedManifestPath);
                }
                else
                {
                    // Fallback: read directly from zip entry before disposal
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        json = reader.ReadToEnd();
                    }
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var manifest = JsonSerializer.Deserialize<ThemeManifest>(json, options);
                return manifest ?? throw new InvalidOperationException(
                    "Failed to parse icon-theme.json — file may be malformed.");
            }
        }
    }

    // ---------------------------------------------------------------
    // Manifest data model (using set not init for netstandard2.0)
    // ---------------------------------------------------------------

    /// <summary>
    /// Top-level descriptor parsed from VS Code's icon-theme.json format.
    /// </summary>
    public class ThemeManifest
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("iconDefinitions")]
        public Dictionary<string, ThemeIconDef>? IconDefinitions { get; set; }

        [JsonPropertyName("folderNames")]
        public Dictionary<string, string>? FolderNames { get; set; }

        [JsonPropertyName("folderNamesExpanded")]
        public Dictionary<string, string>? FolderNamesExpanded { get; set; }

        [JsonPropertyName("fileExtensions")]
        public Dictionary<string, string>? FileExtensions { get; set; }

        [JsonPropertyName("fileNames")]
        public Dictionary<string, string>? FileNames { get; set; }

        [JsonPropertyName("file")]
        public string? DefaultFile { get; set; }

        [JsonPropertyName("folder")]
        public string? DefaultFolder { get; set; }

        [JsonPropertyName("folderExpanded")]
        public string? DefaultFolderExpanded { get; set; }
    }

    /// <summary>An individual icon entry pointing to an image path on disk.</summary>
    public class ThemeIconDef
    {
        [JsonPropertyName("iconPath")]
        public string? IconPath { get; set; }
    }
}
