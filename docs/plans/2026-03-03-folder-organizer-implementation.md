# FolderOrganizer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Windows 11 shell extension + WinUI 3 companion app that lets users right-click files/folders to apply color tints, custom icons, and colored tags — stored in NTFS ADS, non-invasive, never crashes Explorer.

**Architecture:** Four projects in one .NET solution — Core (shared logic), Shell (.NET Framework 4.8 COM DLL for context menu), App (WinUI 3 preset manager), Installer (WiX). Shell and App share Core via a .NET Standard 2.0 library. Metadata lives in NTFS ADS as JSON. Icon presets cached in AppData.

**Tech Stack:** C# / .NET 8 (App), .NET Framework 4.8 (Shell COM DLL), .NET Standard 2.0 (Core), WinUI 3 / Windows App SDK 1.6, System.Drawing.Common (icon tinting), System.Text.Json, WiX Toolset v4, xUnit (tests)

---

## Prerequisites

Install these before starting:
- Visual Studio 2022 (with "Desktop development with C++", ".NET desktop development", "Windows application development" workloads)
- Windows App SDK 1.6 extension for VS
- WiX Toolset v4 + HeatWave VS extension
- .NET 8 SDK
- Git

---

## Task 1: Solution Scaffold

**Files:**
- Create: `FolderOrganizer.sln`
- Create: `src/FolderOrganizer.Core/FolderOrganizer.Core.csproj`
- Create: `src/FolderOrganizer.Shell/FolderOrganizer.Shell.csproj`
- Create: `src/FolderOrganizer.App/FolderOrganizer.App.csproj`
- Create: `tests/FolderOrganizer.Core.Tests/FolderOrganizer.Core.Tests.csproj`

**Step 1: Create solution and projects via CLI**

```powershell
cd C:\Users\parsa\OneDrive\Documents\Docs\GitHub\Folder_organizer

# Solution
dotnet new sln -n FolderOrganizer

# Core (.NET Standard 2.0 so both Framework and modern .NET can reference it)
mkdir -p src/FolderOrganizer.Core
dotnet new classlib -n FolderOrganizer.Core -f netstandard2.0 -o src/FolderOrganizer.Core

# Shell (.NET Framework 4.8 — required for COM shell extensions)
mkdir -p src/FolderOrganizer.Shell
dotnet new classlib -n FolderOrganizer.Shell -f net48 -o src/FolderOrganizer.Shell

# App (WinUI 3) — create via VS template "Blank App, Packaged (WinUI 3 in Desktop)"
# Name: FolderOrganizer.App, placed in src/FolderOrganizer.App

# Tests
mkdir -p tests/FolderOrganizer.Core.Tests
dotnet new xunit -n FolderOrganizer.Core.Tests -f net8.0 -o tests/FolderOrganizer.Core.Tests

# Add all to solution
dotnet sln add src/FolderOrganizer.Core/FolderOrganizer.Core.csproj
dotnet sln add src/FolderOrganizer.Shell/FolderOrganizer.Shell.csproj
dotnet sln add tests/FolderOrganizer.Core.Tests/FolderOrganizer.Core.Tests.csproj
```

**Step 2: Add NuGet packages**

```powershell
# Core
cd src/FolderOrganizer.Core
dotnet add package System.Text.Json --version 8.0.5
dotnet add package System.Drawing.Common --version 8.0.11

# Shell
cd ../FolderOrganizer.Shell
# Add reference to Core (Framework project referencing netstandard2.0 works fine)
dotnet add reference ../FolderOrganizer.Core/FolderOrganizer.Core.csproj

# Tests
cd ../../tests/FolderOrganizer.Core.Tests
dotnet add reference ../../src/FolderOrganizer.Core/FolderOrganizer.Core.csproj
dotnet add package FluentAssertions --version 6.12.0
```

**Step 3: Edit Shell .csproj to enable COM output**

Open `src/FolderOrganizer.Shell/FolderOrganizer.Shell.csproj` and replace contents:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>FolderOrganizer.Shell</RootNamespace>
    <AssemblyName>FolderOrganizer.Shell</AssemblyName>
    <!-- Required for COM registration -->
    <RegisterForCOMInterop>true</RegisterForCOMInterop>
    <ComVisible>false</ComVisible>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\FolderOrganizer.Core\FolderOrganizer.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="System.Drawing" />
  </ItemGroup>
</Project>
```

**Step 4: Delete boilerplate Class1.cs files**

```powershell
Remove-Item src/FolderOrganizer.Core/Class1.cs
Remove-Item src/FolderOrganizer.Shell/Class1.cs
```

**Step 5: Create folder structure**

```powershell
mkdir src/FolderOrganizer.Core/Models
mkdir src/FolderOrganizer.Core/Storage
mkdir src/FolderOrganizer.Core/Icons
mkdir src/FolderOrganizer.Core/Tags
mkdir src/FolderOrganizer.Shell/Com
mkdir src/FolderOrganizer.Shell/Menu
mkdir src/FolderOrganizer.Shell/Drawing
mkdir assets/icons/presets
```

**Step 6: Commit**

```bash
git init
git add .
git commit -m "chore: scaffold solution with Core, Shell, App, Tests projects"
```

---

## Task 2: Core — Metadata Model

**Files:**
- Create: `src/FolderOrganizer.Core/Models/ItemMetadata.cs`
- Create: `src/FolderOrganizer.Core/Models/TagEntry.cs`
- Test: `tests/FolderOrganizer.Core.Tests/Models/ItemMetadataTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/FolderOrganizer.Core.Tests/Models/ItemMetadataTests.cs
using FolderOrganizer.Core.Models;
using FluentAssertions;

namespace FolderOrganizer.Core.Tests.Models;

public class ItemMetadataTests
{
    [Fact]
    public void ItemMetadata_DefaultConstructor_HasNullColorAndIcon()
    {
        var meta = new ItemMetadata();
        meta.Color.Should().BeNull();
        meta.IconPath.Should().BeNull();
        meta.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ItemMetadata_Serialize_RoundTripsCorrectly()
    {
        var meta = new ItemMetadata
        {
            Color = "#EA4335",
            IconPath = @"C:\icons\my.png",
            Tags = new List<TagEntry>
            {
                new TagEntry { Name = "Work", Color = "#1A73E8" }
            }
        };

        var json = meta.ToJson();
        var restored = ItemMetadata.FromJson(json);

        restored.Color.Should().Be("#EA4335");
        restored.IconPath.Should().Be(@"C:\icons\my.png");
        restored.Tags.Should().HaveCount(1);
        restored.Tags[0].Name.Should().Be("Work");
        restored.Tags[0].Color.Should().Be("#1A73E8");
    }

    [Fact]
    public void ItemMetadata_FromJson_WithNull_ReturnsEmptyMetadata()
    {
        var meta = ItemMetadata.FromJson(null);
        meta.Should().NotBeNull();
        meta.Color.Should().BeNull();
    }
}
```

**Step 2: Run test — expect FAIL**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```
Expected: compilation error (types don't exist yet)

**Step 3: Implement the models**

```csharp
// src/FolderOrganizer.Core/Models/TagEntry.cs
namespace FolderOrganizer.Core.Models;

public class TagEntry
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#5F6368";
}
```

```csharp
// src/FolderOrganizer.Core/Models/ItemMetadata.cs
using System.Collections.Generic;
using System.Text.Json;

namespace FolderOrganizer.Core.Models;

public class ItemMetadata
{
    public string? Color { get; set; }
    public string? IconPath { get; set; }
    public List<TagEntry> Tags { get; set; } = new();

    public bool IsEmpty => Color is null && IconPath is null && Tags.Count == 0;

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public static ItemMetadata FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ItemMetadata();

        try
        {
            return JsonSerializer.Deserialize<ItemMetadata>(json) ?? new ItemMetadata();
        }
        catch
        {
            return new ItemMetadata();
        }
    }
}
```

**Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```
Expected: 3 tests pass

**Step 5: Commit**

```bash
git add src/FolderOrganizer.Core/Models/ tests/
git commit -m "feat(core): add ItemMetadata and TagEntry models with JSON serialization"
```

---

## Task 3: Core — NTFS ADS Storage

**Files:**
- Create: `src/FolderOrganizer.Core/Storage/AdsStorage.cs`
- Create: `src/FolderOrganizer.Core/Storage/TouchLog.cs`
- Test: `tests/FolderOrganizer.Core.Tests/Storage/AdsStorageTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/FolderOrganizer.Core.Tests/Storage/AdsStorageTests.cs
using FolderOrganizer.Core.Models;
using FolderOrganizer.Core.Storage;
using FluentAssertions;

namespace FolderOrganizer.Core.Tests.Storage;

public class AdsStorageTests : IDisposable
{
    private readonly string _testFolder;

    public AdsStorageTests()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), "FolderOrganizerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testFolder);
    }

    [Fact]
    public void ReadMetadata_WhenNoAdsExists_ReturnsEmptyMetadata()
    {
        var storage = new AdsStorage();
        var meta = storage.ReadMetadata(_testFolder);
        meta.Should().NotBeNull();
        meta.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void WriteAndRead_RoundTrips()
    {
        var storage = new AdsStorage();
        var original = new ItemMetadata { Color = "#EA4335" };

        storage.WriteMetadata(_testFolder, original);
        var read = storage.ReadMetadata(_testFolder);

        read.Color.Should().Be("#EA4335");
    }

    [Fact]
    public void ClearMetadata_RemovesAdsStream()
    {
        var storage = new AdsStorage();
        storage.WriteMetadata(_testFolder, new ItemMetadata { Color = "#EA4335" });
        storage.ClearMetadata(_testFolder);
        var meta = storage.ReadMetadata(_testFolder);
        meta.IsEmpty.Should().BeTrue();
    }

    public void Dispose() => Directory.Delete(_testFolder, true);
}
```

**Step 2: Run test — expect FAIL**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```

**Step 3: Implement AdsStorage**

The ADS stream name is `FolderOrganizer`. For folders, the ADS is on the folder itself (`C:\MyFolder:FolderOrganizer`). For files it's `C:\file.txt:FolderOrganizer`.

Atomic write: write to `path:FolderOrganizer_tmp`, then delete `path:FolderOrganizer`, then rename. On Windows, ADS can't be renamed directly, so we write to temp ADS and copy.

```csharp
// src/FolderOrganizer.Core/Storage/AdsStorage.cs
using System;
using System.IO;
using System.Text;
using FolderOrganizer.Core.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace FolderOrganizer.Core.Storage;

public class AdsStorage
{
    private const string StreamName = "FolderOrganizer";

    public ItemMetadata ReadMetadata(string path)
    {
        try
        {
            var adsPath = $"{path}:{StreamName}";
            if (!FileExistsAds(adsPath))
                return new ItemMetadata();

            using var fs = new FileStream(adsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var json = reader.ReadToEnd();
            return ItemMetadata.FromJson(json);
        }
        catch
        {
            return new ItemMetadata();
        }
    }

    public void WriteMetadata(string path, ItemMetadata metadata)
    {
        // Atomic: write to temp stream, then overwrite main stream
        var adsPath = $"{path}:{StreamName}";
        var tempAdsPath = $"{path}:{StreamName}_tmp";

        try
        {
            var json = metadata.ToJson();
            var bytes = Encoding.UTF8.GetBytes(json);

            // Write to temp stream
            using (var fs = new FileStream(tempAdsPath, FileMode.Create, FileAccess.Write, FileShare.None))
                fs.Write(bytes, 0, bytes.Length);

            // Overwrite main stream
            using (var fs = new FileStream(adsPath, FileMode.Create, FileAccess.Write, FileShare.None))
                fs.Write(bytes, 0, bytes.Length);

            // Clean up temp stream
            DeleteAds(tempAdsPath);
        }
        catch
        {
            // Never leave partial state — swallow silently
            try { DeleteAds(tempAdsPath); } catch { }
        }
    }

    public void ClearMetadata(string path)
    {
        try
        {
            DeleteAds($"{path}:{StreamName}");
        }
        catch { }
    }

    private static bool FileExistsAds(string adsPath)
    {
        try
        {
            using var fs = new FileStream(adsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteAds(string adsPath)
    {
        try { File.Delete(adsPath); } catch { }
    }
}
```

```csharp
// src/FolderOrganizer.Core/Storage/TouchLog.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderOrganizer.Core.Storage;

/// <summary>
/// Tracks every path the app has modified, enabling "Clear All" from the settings app.
/// </summary>
public class TouchLog
{
    private readonly string _logPath;

    public TouchLog(string appDataPath)
    {
        _logPath = Path.Combine(appDataPath, "touched.log");
    }

    public void Record(string path)
    {
        try
        {
            File.AppendAllText(_logPath, path + Environment.NewLine);
        }
        catch { }
    }

    public IEnumerable<string> GetAllTouchedPaths()
    {
        try
        {
            if (!File.Exists(_logPath))
                return Enumerable.Empty<string>();

            return File.ReadAllLines(_logPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public void Clear()
    {
        try { File.Delete(_logPath); } catch { }
    }
}
```

**Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```

**Step 5: Commit**

```bash
git add src/FolderOrganizer.Core/Storage/ tests/
git commit -m "feat(core): add ADS metadata storage with atomic writes and touch log"
```

---

## Task 4: Core — Icon Generation

**Files:**
- Create: `src/FolderOrganizer.Core/Icons/IconGenerator.cs`
- Create: `src/FolderOrganizer.Core/Icons/ChromePalette.cs`
- Create: `src/FolderOrganizer.Core/Icons/IconCache.cs`
- Test: `tests/FolderOrganizer.Core.Tests/Icons/IconGeneratorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/FolderOrganizer.Core.Tests/Icons/IconGeneratorTests.cs
using FolderOrganizer.Core.Icons;
using FluentAssertions;
using System.Drawing;

namespace FolderOrganizer.Core.Tests.Icons;

public class IconGeneratorTests
{
    [Fact]
    public void ChromePalette_HasNineColors()
    {
        ChromePalette.Colors.Should().HaveCount(9);
    }

    [Fact]
    public void ChromePalette_AllColorsAreValid()
    {
        foreach (var (name, hex) in ChromePalette.Colors)
        {
            var color = ColorTranslator.FromHtml(hex);
            color.IsEmpty.Should().BeFalse(because: $"{name} ({hex}) should be a valid color");
        }
    }

    [Fact]
    public void IconGenerator_TintBitmap_ProducesDifferentResult()
    {
        using var source = new Bitmap(32, 32);
        // Fill with grey
        using (var g = Graphics.FromImage(source))
            g.Clear(Color.Gray);

        var tinted = IconGenerator.TintBitmap(source, Color.Red, 0.7f);
        tinted.Should().NotBeNull();
        tinted.Width.Should().Be(32);
        tinted.Height.Should().Be(32);

        // The tinted image should differ from the source
        var srcPixel = source.GetPixel(16, 16);
        var dstPixel = tinted.GetPixel(16, 16);
        dstPixel.Should().NotBe(srcPixel);
    }
}
```

**Step 2: Run — expect FAIL**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```

**Step 3: Implement**

```csharp
// src/FolderOrganizer.Core/Icons/ChromePalette.cs
using System.Collections.Generic;

namespace FolderOrganizer.Core.Icons;

public static class ChromePalette
{
    /// <summary>Chrome tab group colors in order.</summary>
    public static readonly IReadOnlyList<(string Name, string Hex)> Colors = new[]
    {
        ("Grey",   "#5F6368"),
        ("Blue",   "#1A73E8"),
        ("Red",    "#EA4335"),
        ("Yellow", "#FBBC04"),
        ("Green",  "#34A853"),
        ("Pink",   "#F06292"),
        ("Purple", "#9334E6"),
        ("Cyan",   "#24C1E0"),
        ("Orange", "#FA903E"),
    };
}
```

```csharp
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
            float a = buffer[i + 3] / 255f;

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
```

```csharp
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
```

**Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/FolderOrganizer.Core.Tests/ -v minimal
```

Note: `IconGeneratorTests.IconGenerator_TintBitmap_ProducesDifferentResult` should pass. The SHGetFileInfo tests are manual (require Windows shell).

**Step 5: Commit**

```bash
git add src/FolderOrganizer.Core/Icons/ tests/
git commit -m "feat(core): add icon generation, Chrome palette, and AppData icon cache"
```

---

## Task 5: Core — Tag Registry

**Files:**
- Create: `src/FolderOrganizer.Core/Tags/TagRegistry.cs`
- Test: `tests/FolderOrganizer.Core.Tests/Tags/TagRegistryTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/FolderOrganizer.Core.Tests/Tags/TagRegistryTests.cs
using FolderOrganizer.Core.Models;
using FolderOrganizer.Core.Tags;
using FluentAssertions;

namespace FolderOrganizer.Core.Tests.Tags;

public class TagRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TagRegistry _registry;

    public TagRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FolderOrganizerTagTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _registry = new TagRegistry(_tempDir);
    }

    [Fact]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        _registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void AddTag_ThenGetAll_ContainsTag()
    {
        _registry.AddOrUpdate(new TagEntry { Name = "Work", Color = "#1A73E8" });
        var tags = _registry.GetAll();
        tags.Should().HaveCount(1);
        tags[0].Name.Should().Be("Work");
    }

    [Fact]
    public void AddTag_SameName_UpdatesExisting()
    {
        _registry.AddOrUpdate(new TagEntry { Name = "Work", Color = "#1A73E8" });
        _registry.AddOrUpdate(new TagEntry { Name = "Work", Color = "#EA4335" });
        var tags = _registry.GetAll();
        tags.Should().HaveCount(1);
        tags[0].Color.Should().Be("#EA4335");
    }

    [Fact]
    public void DeleteTag_RemovesIt()
    {
        _registry.AddOrUpdate(new TagEntry { Name = "Work", Color = "#1A73E8" });
        _registry.Delete("Work");
        _registry.GetAll().Should().BeEmpty();
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
```

**Step 2: Run — expect FAIL**

**Step 3: Implement**

```csharp
// src/FolderOrganizer.Core/Tags/TagRegistry.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FolderOrganizer.Core.Models;

namespace FolderOrganizer.Core.Tags;

public class TagRegistry
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public TagRegistry(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "tags.json");
    }

    public List<TagEntry> GetAll()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath)) return new List<TagEntry>();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<TagEntry>>(json) ?? new List<TagEntry>();
            }
            catch { return new List<TagEntry>(); }
        }
    }

    public void AddOrUpdate(TagEntry tag)
    {
        lock (_lock)
        {
            var tags = GetAll();
            var existing = tags.FirstOrDefault(t => t.Name == tag.Name);
            if (existing is not null)
                existing.Color = tag.Color;
            else
                tags.Add(tag);
            Save(tags);
        }
    }

    public void Delete(string name)
    {
        lock (_lock)
        {
            var tags = GetAll().Where(t => t.Name != name).ToList();
            Save(tags);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            try { File.Delete(_filePath); } catch { }
        }
    }

    private void Save(List<TagEntry> tags)
    {
        var json = JsonSerializer.Serialize(tags, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

**Step 4: Run — expect PASS**

**Step 5: Commit**

```bash
git add src/FolderOrganizer.Core/Tags/ tests/
git commit -m "feat(core): add TagRegistry with add/update/delete persistence"
```

---

## Task 6: Core — AppDataPaths Helper

**Files:**
- Create: `src/FolderOrganizer.Core/AppDataPaths.cs`

This centralizes all AppData paths so both Shell and App agree on locations.

**Step 1: Implement (no test needed — it's just path strings)**

```csharp
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
```

**Step 2: Commit**

```bash
git add src/FolderOrganizer.Core/AppDataPaths.cs
git commit -m "feat(core): add AppDataPaths central path registry"
```

---

## Task 7: Shell Extension — COM Skeleton

**Files:**
- Create: `src/FolderOrganizer.Shell/Com/Guids.cs`
- Create: `src/FolderOrganizer.Shell/Com/ComInterfaces.cs`
- Create: `src/FolderOrganizer.Shell/Com/FolderOrganizerContextMenu.cs`

This task creates a working but text-only shell extension that you can register and test in Explorer.

**Step 1: Define GUIDs (generate fresh ones)**

```csharp
// src/FolderOrganizer.Shell/Com/Guids.cs
namespace FolderOrganizer.Shell.Com;

// Generate fresh GUIDs: Tools > Create GUID in VS, or use `[Guid(Guid.NewGuid().ToString())]` won't work at compile time
// Use these fixed GUIDs — do not change after first release (breaks COM registration)
internal static class Guids
{
    // Run: PS> [System.Guid]::NewGuid() to get your own GUIDs
    public const string ContextMenu = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890"; // REPLACE with your generated GUID
}
```

> **Important:** Replace `"A1B2C3D4-..."` with an actual GUID generated via PowerShell: `[System.Guid]::NewGuid()`

**Step 2: Define COM interfaces via P/Invoke**

```csharp
// src/FolderOrganizer.Shell/Com/ComInterfaces.cs
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FolderOrganizer.Shell.Com;

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214e8-0000-0000-c000-000000000046")]
internal interface IShellExtInit
{
    void Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214e4-0000-0000-c000-000000000046")]
internal interface IContextMenu
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    void InvokeCommand(IntPtr pici);
    void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214f4-0000-0000-c000-000000000046")]
internal interface IContextMenu2 : IContextMenu
{
    [PreserveSig]
    new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    new void InvokeCommand(IntPtr pici);
    new void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
    void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
}
```

**Step 3: Implement the COM class (text-only first)**

```csharp
// src/FolderOrganizer.Shell/Com/FolderOrganizerContextMenu.cs
using System;
using System.Runtime.InteropServices;
using System.Text;
using FolderOrganizer.Core;
using FolderOrganizer.Core.Models;
using FolderOrganizer.Core.Storage;

namespace FolderOrganizer.Shell.Com;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid(Guids.ContextMenu)]
[ProgId("FolderOrganizer.ContextMenu")]
public class FolderOrganizerContextMenu : IShellExtInit, IContextMenu2
{
    private string _selectedPath = string.Empty;
    private readonly AdsStorage _ads = new();

    // Command IDs (offsets from idCmdFirst)
    private const int CMD_ORGANIZE = 0;

    #region IShellExtInit
    public void Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID)
    {
        try
        {
            if (pDataObj == IntPtr.Zero)
            {
                // Called on a folder background — get folder from pidlFolder
                _selectedPath = GetPathFromPidl(pidlFolder);
                return;
            }

            // Get path from IDataObject
            var dataObj = Marshal.GetObjectForIUnknown(pDataObj) as System.Runtime.InteropServices.ComTypes.IDataObject;
            if (dataObj is null) return;

            _selectedPath = GetPathFromDataObject(dataObj);
        }
        catch { /* never propagate */ }
    }
    #endregion

    #region IContextMenu
    public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
    {
        try
        {
            if ((uFlags & 0xF) == 0x10) // CMF_DEFAULTONLY
                return WinError.MAKE_HRESULT(0, 0, 0);

            // Insert "Organize" top-level item with a submenu
            var hSubMenu = CreatePopupMenu();

            InsertMenu(hSubMenu, 0, MF_BYPOSITION | MF_STRING, idCmdFirst + 1, "Set Color...");
            InsertMenu(hSubMenu, 1, MF_BYPOSITION | MF_STRING, idCmdFirst + 2, "Set Icon...");
            InsertMenu(hSubMenu, 2, MF_BYPOSITION | MF_STRING, idCmdFirst + 3, "Edit Tags...");
            InsertMenu(hSubMenu, 3, MF_BYPOSITION | MF_SEPARATOR, 0, null);
            InsertMenu(hSubMenu, 4, MF_BYPOSITION | MF_STRING, idCmdFirst + 4, "Clear Color");
            InsertMenu(hSubMenu, 5, MF_BYPOSITION | MF_STRING, idCmdFirst + 5, "Clear Icon");
            InsertMenu(hSubMenu, 6, MF_BYPOSITION | MF_STRING, idCmdFirst + 6, "Clear Tags");
            InsertMenu(hSubMenu, 7, MF_BYPOSITION | MF_STRING, idCmdFirst + 7, "Clear All");

            var mii = new MENUITEMINFO();
            mii.cbSize = (uint)Marshal.SizeOf(mii);
            mii.fMask = MIIM_SUBMENU | MIIM_STRING | MIIM_ID;
            mii.wID = idCmdFirst + CMD_ORGANIZE;
            mii.hSubMenu = hSubMenu;
            mii.dwTypeData = "Organize";
            InsertMenuItemW(hmenu, indexMenu, true, ref mii);

            return WinError.MAKE_HRESULT(0, 0, 8); // 8 commands added
        }
        catch
        {
            return WinError.MAKE_HRESULT(1, 0, 0);
        }
    }

    public void InvokeCommand(IntPtr pici)
    {
        try
        {
            var ici = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
            var cmdOffset = (int)ici.lpVerb;

            switch (cmdOffset)
            {
                case 1: // Set Color — launch app to color picker
                    LaunchApp($"--action setcolor --path \"{_selectedPath}\"");
                    break;
                case 2: // Set Icon
                    LaunchApp($"--action seticon --path \"{_selectedPath}\"");
                    break;
                case 3: // Edit Tags
                    LaunchApp($"--action settags --path \"{_selectedPath}\"");
                    break;
                case 4: _ads.ClearField(_selectedPath, "color"); break;
                case 5: _ads.ClearField(_selectedPath, "icon"); break;
                case 6: _ads.ClearField(_selectedPath, "tags"); break;
                case 7: _ads.ClearMetadata(_selectedPath); break;
            }
        }
        catch { }
    }

    public void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax)
    {
        try
        {
            if ((uType & 0x4) != 0) // GCS_UNICODE
                pszName.Append("FolderOrganizer");
        }
        catch { }
    }

    public void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam) { /* Phase 2: owner draw */ }
    #endregion

    #region COM Registration
    [ComRegisterFunction]
    public static void Register(Type t)
    {
        // Register for Files
        using var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
            $@"*\shellex\ContextMenuHandlers\FolderOrganizer");
        key?.SetValue("", $"{{{Guids.ContextMenu}}}");

        // Register for Folders
        using var folderKey = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
            $@"Folder\shellex\ContextMenuHandlers\FolderOrganizer");
        folderKey?.SetValue("", $"{{{Guids.ContextMenu}}}");

        // Register for Directory
        using var dirKey = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
            $@"Directory\shellex\ContextMenuHandlers\FolderOrganizer");
        dirKey?.SetValue("", $"{{{Guids.ContextMenu}}}");
    }

    [ComUnregisterFunction]
    public static void Unregister(Type t)
    {
        Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
            $@"*\shellex\ContextMenuHandlers\FolderOrganizer", false);
        Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
            $@"Folder\shellex\ContextMenuHandlers\FolderOrganizer", false);
        Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
            $@"Directory\shellex\ContextMenuHandlers\FolderOrganizer", false);
    }
    #endregion

    #region Helpers
    private static void LaunchApp(string args)
    {
        var appPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "FolderOrganizer", "FolderOrganizer.App.exe");
        if (System.IO.File.Exists(appPath))
            System.Diagnostics.Process.Start(appPath, args);
    }

    private static string GetPathFromPidl(IntPtr pidl)
    {
        var sb = new StringBuilder(260);
        SHGetPathFromIDList(pidl, sb);
        return sb.ToString();
    }

    private static string GetPathFromDataObject(System.Runtime.InteropServices.ComTypes.IDataObject dataObj)
    {
        var fmt = new System.Runtime.InteropServices.ComTypes.FORMATETC
        {
            cfFormat = CF_HDROP,
            ptd = IntPtr.Zero,
            dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
        };
        dataObj.GetData(ref fmt, out var stg);
        var hDrop = Marshal.GetIUnknownForObject(stg.unionmember);
        var sb = new StringBuilder(260);
        DragQueryFile(stg.unionmember, 0, sb, 260);
        return sb.ToString();
    }

    [DllImport("shell32.dll")] static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);
    [DllImport("shell32.dll")] static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool InsertMenuItemW(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

    private const ushort CF_HDROP = 15;
    private const uint MF_BYPOSITION = 0x400;
    private const uint MF_STRING = 0x0;
    private const uint MF_SEPARATOR = 0x800;
    private const uint MIIM_SUBMENU = 0x4;
    private const uint MIIM_STRING = 0x40;
    private const uint MIIM_ID = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MENUITEMINFO
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public IntPtr dwItemData;
        public string dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }
    #endregion
}

internal static class WinError
{
    public static int MAKE_HRESULT(uint sev, uint fac, uint code) =>
        (int)((sev << 31) | (fac << 16) | code);
}
```

> **Note:** You'll need to add `ClearField` to `AdsStorage` — a helper that reads metadata, clears one field, and writes back.

**Step 4: Add ClearField to AdsStorage**

```csharp
// In AdsStorage.cs, add:
public void ClearField(string path, string field)
{
    var meta = ReadMetadata(path);
    switch (field)
    {
        case "color": meta.Color = null; break;
        case "icon": meta.IconPath = null; break;
        case "tags": meta.Tags.Clear(); break;
    }
    if (meta.IsEmpty)
        ClearMetadata(path);
    else
        WriteMetadata(path, meta);
}
```

**Step 5: Build and test register manually**

```powershell
# Build
dotnet build src/FolderOrganizer.Shell/ -c Release

# Register (run as Administrator)
regsvr32 src\FolderOrganizer.Shell\bin\Release\net48\FolderOrganizer.Shell.dll

# Right-click any folder — you should see "Organize" in the menu
# To unregister:
regsvr32 /u src\FolderOrganizer.Shell\bin\Release\net48\FolderOrganizer.Shell.dll
```

**Step 6: Commit**

```bash
git add src/FolderOrganizer.Shell/
git commit -m "feat(shell): add COM context menu skeleton with text-only menu items"
```

---

## Task 8: Shell Extension — Owner-Drawn Color Circles

**Files:**
- Create: `src/FolderOrganizer.Shell/Drawing/MenuRenderer.cs`
- Modify: `src/FolderOrganizer.Shell/Com/FolderOrganizerContextMenu.cs`

Replace the text "Set Color..." item with an owner-drawn row of 9 colored circles + a `+` button.

**Step 1: Implement MenuRenderer**

```csharp
// src/FolderOrganizer.Shell/Drawing/MenuRenderer.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using FolderOrganizer.Core.Icons;

namespace FolderOrganizer.Shell.Drawing;

internal static class MenuRenderer
{
    public const int CIRCLE_SIZE = 20;
    public const int CIRCLE_PADDING = 6;
    public const int ROW_HEIGHT = 36;
    public const int ROW_PADDING_LEFT = 28;

    public static Size MeasureColorRow()
    {
        int count = ChromePalette.Colors.Count + 1; // +1 for the "+" button
        int width = ROW_PADDING_LEFT + count * (CIRCLE_SIZE + CIRCLE_PADDING) + CIRCLE_PADDING + 60;
        return new Size(width, ROW_HEIGHT);
    }

    public static void DrawColorRow(IntPtr hdc, Rectangle bounds, string? activeColor)
    {
        using var g = Graphics.FromHdc(hdc);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background (match menu background)
        using var bgBrush = new SolidBrush(SystemColors.Menu);
        g.FillRectangle(bgBrush, bounds);

        // Label
        using var font = new Font("Segoe UI", 9f);
        using var textBrush = new SolidBrush(SystemColors.MenuText);
        g.DrawString("Color", font, textBrush, bounds.Left + 6, bounds.Top + (ROW_HEIGHT - 14) / 2);

        int x = bounds.Left + ROW_PADDING_LEFT;
        int y = bounds.Top + (ROW_HEIGHT - CIRCLE_SIZE) / 2;

        foreach (var (_, hex) in ChromePalette.Colors)
        {
            var color = ColorTranslator.FromHtml(hex);
            using var brush = new SolidBrush(color);
            var rect = new Rectangle(x, y, CIRCLE_SIZE, CIRCLE_SIZE);
            g.FillEllipse(brush, rect);

            // Checkmark if active
            if (hex.Equals(activeColor, StringComparison.OrdinalIgnoreCase))
            {
                using var pen = new Pen(Color.White, 2f);
                g.DrawLine(pen, x + 5, y + 10, x + 9, y + 14);
                g.DrawLine(pen, x + 9, y + 14, x + 15, y + 6);
            }

            // Hover border (drawn when item is selected — check ODA_SELECT)
            x += CIRCLE_SIZE + CIRCLE_PADDING;
        }

        // "+" button
        using var plusBrush = new SolidBrush(Color.FromArgb(100, 128, 128, 128));
        using var plusPen = new Pen(Color.Gray, 1.5f);
        var plusRect = new Rectangle(x, y, CIRCLE_SIZE, CIRCLE_SIZE);
        g.FillEllipse(plusBrush, plusRect);
        g.DrawEllipse(plusPen, plusRect);
        using var plusFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        g.DrawString("+", plusFont, new SolidBrush(Color.White),
            x + CIRCLE_SIZE / 2 - 5, y + CIRCLE_SIZE / 2 - 8);
    }

    public static int HitTestColorCircle(Point cursor, Rectangle bounds)
    {
        // Returns: 0-8 for Chrome colors, 9 for "+" button, -1 for miss
        int x = bounds.Left + ROW_PADDING_LEFT;
        int y = bounds.Top + (ROW_HEIGHT - CIRCLE_SIZE) / 2;

        for (int i = 0; i <= ChromePalette.Colors.Count; i++) // +1 for "+"
        {
            var rect = new Rectangle(x, y, CIRCLE_SIZE, CIRCLE_SIZE);
            var center = new Point(rect.Left + CIRCLE_SIZE / 2, rect.Top + CIRCLE_SIZE / 2);
            var dist = Math.Sqrt(Math.Pow(cursor.X - center.X, 2) + Math.Pow(cursor.Y - center.Y, 2));
            if (dist <= CIRCLE_SIZE / 2.0)
                return i;
            x += CIRCLE_SIZE + CIRCLE_PADDING;
        }
        return -1;
    }
}
```

**Step 2: Update QueryContextMenu to use owner-drawn items for color row**

In `FolderOrganizerContextMenu.cs`, modify the submenu setup to use `MF_OWNERDRAW` for the color row item and handle `HandleMenuMsg` to respond to `WM_MEASUREITEM` and `WM_DRAWITEM`.

> This is the most complex part. The owner-draw messages (WM_MEASUREITEM = 0x2C, WM_DRAWITEM = 0x2B) are forwarded to the extension via `IContextMenu2.HandleMenuMsg`. You store the menu item data pointer as `dwItemData` in `MENUITEMINFO` to identify which item is being drawn.

Full updated `FolderOrganizerContextMenu.cs` with owner-draw support: see `docs/references/owner-draw-context-menu.md` (a well-documented pattern — search "IContextMenu2 owner draw C#" for reference implementations).

**Step 3: Build and test visually**

```powershell
dotnet build src/FolderOrganizer.Shell/ -c Release
# Re-register (if already registered, regsvr32 /u first, then regsvr32 again)
```

Right-click a folder — you should see the color circle row rendered inside the Organize submenu.

**Step 4: Commit**

```bash
git add src/FolderOrganizer.Shell/
git commit -m "feat(shell): add owner-drawn color circle row in context menu"
```

---

## Task 9: Shell Extension — Icon Grid & Tags Row

Follow the same owner-draw pattern from Task 8.

**Icon grid item:** Read `CustomIconsDir` on menu open, load thumbnails (32x32 scaled), render as a grid of squares. Hit-test on click returns the index.

**Tags row:** Read `TagRegistry.GetAll()` on menu open. Render colored pill shapes with tag name text. Currently applied tags (read from ADS) show with a border/bold. Hit-test on click returns tag index.

**Step 1: Add `LoadPreviewThumbnail` to IconGenerator**

```csharp
public static Bitmap? LoadPreviewThumbnail(string path, int size = 32)
{
    try
    {
        using var original = new Bitmap(path);
        var thumb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(thumb);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, size, size);
        return thumb;
    }
    catch { return null; }
}
```

**Step 2: Add tag pill renderer to MenuRenderer**

```csharp
public static void DrawTagRow(IntPtr hdc, Rectangle bounds, List<TagEntry> allTags, List<string> activeTags)
{
    using var g = Graphics.FromHdc(hdc);
    g.SmoothingMode = SmoothingMode.AntiAlias;

    using var bgBrush = new SolidBrush(SystemColors.Menu);
    g.FillRectangle(bgBrush, bounds);

    using var font = new Font("Segoe UI", 8.5f);
    int x = bounds.Left + ROW_PADDING_LEFT;
    int y = bounds.Top + 6;

    foreach (var tag in allTags)
    {
        var color = ColorTranslator.FromHtml(tag.Color);
        var textSize = g.MeasureString(tag.Name, font);
        int pillWidth = (int)textSize.Width + 12;
        int pillHeight = 20;
        var pillRect = new RectangleF(x, y, pillWidth, pillHeight);

        bool isActive = activeTags.Contains(tag.Name);
        using var fillBrush = new SolidBrush(isActive ? color : Color.FromArgb(40, color));
        g.FillRoundedRectangle(fillBrush, pillRect, 10);

        using var textBrush = new SolidBrush(isActive ? Color.White : color);
        g.DrawString(tag.Name, font, textBrush, x + 6, y + 3);
        x += pillWidth + 6;
    }
}
```

Note: `FillRoundedRectangle` is a GDI+ extension — add a helper:
```csharp
static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, float radius)
{
    using var path = new GraphicsPath();
    path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
    path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
    path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
    path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
    path.CloseAllFigures();
    g.FillPath(brush, path);
}
```

**Step 3: Wire up InvokeCommand for tags (toggle)**

```csharp
case CMD_TAG_BASE + tagIndex:
    var meta = _ads.ReadMetadata(_selectedPath);
    var tagName = _loadedTags[tagIndex].Name;
    var existing = meta.Tags.FirstOrDefault(t => t.Name == tagName);
    if (existing is not null)
        meta.Tags.Remove(existing);
    else
        meta.Tags.Add(_loadedTags[tagIndex]);
    _ads.WriteMetadata(_selectedPath, meta);
    ApplyFolderIcon(_selectedPath, meta);
    break;
```

**Step 4: Apply icon to folder via desktop.ini**

When a color or custom icon is applied, write a `desktop.ini` to the folder setting `IconFile` and `IconIndex`:

```csharp
private void ApplyFolderIcon(string folderPath, ItemMetadata meta)
{
    if (!Directory.Exists(folderPath)) return;

    var iconPath = meta.IconPath
        ?? (meta.Color is not null ? _iconCache.GetOrGenerateIcon(meta.Color) : null);

    var desktopIni = Path.Combine(folderPath, "desktop.ini");

    if (iconPath is null)
    {
        // Remove customization
        if (File.Exists(desktopIni))
        {
            RemoveHidden(desktopIni);
            // Remove [.ShellClassInfo] section only
            var lines = File.ReadAllLines(desktopIni)
                .Where(l => !l.StartsWith("IconFile=") && !l.StartsWith("IconIndex=") && l != "[.ShellClassInfo]")
                .ToArray();
            if (lines.Length == 0) File.Delete(desktopIni);
            else File.WriteAllLines(desktopIni, lines);
        }
        RemoveSystemAttribute(folderPath);
        return;
    }

    // Write desktop.ini
    var ini = $"[.ShellClassInfo]\r\nIconFile={iconPath}\r\nIconIndex=0\r\n";
    File.WriteAllText(desktopIni, ini);
    SetHidden(desktopIni);
    SetSystem(folderPath); // Folders must have System attribute for desktop.ini to be read

    // Notify shell to refresh
    SHChangeNotify(0x08000000, 0x0005, folderPath, null); // SHCNE_UPDATEDIR
}

[DllImport("shell32.dll")]
static extern void SHChangeNotify(uint wEventId, uint uFlags, string dwItem1, string dwItem2);
```

**Step 5: Commit**

```bash
git add src/FolderOrganizer.Shell/
git commit -m "feat(shell): add owner-drawn icon grid and tag pills, apply icons via desktop.ini"
```

---

## Task 10: WinUI 3 App — Scaffold & Navigation

**Files:**
- Modify: `src/FolderOrganizer.App/MainWindow.xaml`
- Modify: `src/FolderOrganizer.App/MainWindow.xaml.cs`
- Create: `src/FolderOrganizer.App/Views/PresetsPage.xaml` + `.cs`
- Create: `src/FolderOrganizer.App/Views/ThemesPage.xaml` + `.cs`
- Create: `src/FolderOrganizer.App/Views/SettingsPage.xaml` + `.cs`

**Step 1: Set up MainWindow with NavigationView**

```xml
<!-- src/FolderOrganizer.App/MainWindow.xaml -->
<Window x:Class="FolderOrganizer.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Folder Organizer">

    <NavigationView x:Name="NavView"
                    IsSettingsVisible="False"
                    PaneDisplayMode="Left"
                    SelectionChanged="NavView_SelectionChanged">
        <NavigationView.MenuItems>
            <NavigationViewItem Content="Presets" Tag="Presets">
                <NavigationViewItem.Icon>
                    <FontIcon Glyph="&#xE790;" />
                </NavigationViewItem.Icon>
            </NavigationViewItem>
            <NavigationViewItem Content="Themes" Tag="Themes">
                <NavigationViewItem.Icon>
                    <FontIcon Glyph="&#xE771;" />
                </NavigationViewItem.Icon>
            </NavigationViewItem>
        </NavigationView.MenuItems>
        <NavigationView.FooterMenuItems>
            <NavigationViewItem Content="Settings" Tag="Settings">
                <NavigationViewItem.Icon>
                    <FontIcon Glyph="&#xE713;" />
                </NavigationViewItem.Icon>
            </NavigationViewItem>
        </NavigationView.FooterMenuItems>

        <Frame x:Name="ContentFrame" />
    </NavigationView>
</Window>
```

```csharp
// MainWindow.xaml.cs
private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
{
    var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
    ContentFrame.Navigate(tag switch
    {
        "Presets" => typeof(PresetsPage),
        "Themes" => typeof(ThemesPage),
        "Settings" => typeof(SettingsPage),
        _ => typeof(PresetsPage)
    });
}

private void Window_Activated(object sender, WindowActivatedEventArgs args)
{
    NavView.SelectedItem = NavView.MenuItems[0]; // Default to Presets
}
```

**Step 2: Create stub pages**

Each page is just a `Page` with a `TextBlock` for now. Flesh out in Tasks 11-13.

**Step 3: Handle command-line arguments (from shell extension)**

In `App.xaml.cs`:
```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    var cmdArgs = Environment.GetCommandLineArgs();
    // Parse --action and --path, navigate to correct page and open dialog
    _window = new MainWindow(cmdArgs);
    _window.Activate();
}
```

**Step 4: Commit**

```bash
git add src/FolderOrganizer.App/
git commit -m "feat(app): scaffold WinUI 3 app with NavigationView and three pages"
```

---

## Task 11: App — Presets Page

**Files:**
- Modify: `src/FolderOrganizer.App/Views/PresetsPage.xaml` + `.cs`

Build the three Expander sections: Colors, Icons, Tags.

**Step 1: Colors section**

```xml
<Expander Header="Colors" IsExpanded="True">
    <ItemsRepeater ItemsSource="{x:Bind ViewModel.Colors}">
        <ItemsRepeater.Layout>
            <WrapLayout Orientation="Horizontal" ItemSpacing="8" LineSpacing="8"/>
        </ItemsRepeater.Layout>
        <ItemsRepeater.ItemTemplate>
            <DataTemplate x:DataType="vm:ColorPresetViewModel">
                <Grid Width="40" Height="40">
                    <Ellipse Width="32" Height="32"
                             Fill="{x:Bind HexColor, Converter={StaticResource HexToSolidBrush}}" />
                    <Button Visibility="{x:Bind IsCustom, Converter={StaticResource BoolToVisible}}"
                            Content="&#xE711;" FontFamily="Segoe Fluent Icons"
                            Command="{x:Bind DeleteCommand}" />
                </Grid>
            </DataTemplate>
        </ItemsRepeater.Layout>
    </ItemsRepeater>
</Expander>
```

Use `ColorPicker` (WinUI built-in) in a `ContentDialog` when user hits `+`.

**Step 2: Icons section**

`GridView` of `Image` controls loaded from `CustomIconsDir`. Delete button overlay on each. `+` button opens `FileOpenPicker`.

**Step 3: Tags section**

`ListView` of tag rows — each row: colored pill preview, name `TextBox`, color circle, delete button. `+` at bottom adds new row.

**Step 4: Commit**

```bash
git add src/FolderOrganizer.App/Views/PresetsPage*
git commit -m "feat(app): implement Presets page with colors, icons, and tags sections"
```

---

## Task 12: App — Themes Page

**Files:**
- Modify: `src/FolderOrganizer.App/Views/ThemesPage.xaml` + `.cs`
- Create: `src/FolderOrganizer.Core/Themes/VsixThemeImporter.cs`

**Step 1: Implement VsixThemeImporter in Core**

```csharp
// src/FolderOrganizer.Core/Themes/VsixThemeImporter.cs
using System.IO.Compression;
using System.Text.Json;

namespace FolderOrganizer.Core.Themes;

public class VsixThemeImporter
{
    public ThemeManifest Import(string vsixOrZipPath, string extractTo)
    {
        using var zip = ZipFile.OpenRead(vsixOrZipPath);
        // Find package.json or icon-theme.json
        var manifestEntry = zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith("icon-theme.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry is null)
            throw new InvalidOperationException("No icon-theme.json found in archive");

        Directory.CreateDirectory(extractTo);
        zip.ExtractToDirectory(extractTo, overwriteFiles: true);

        using var stream = manifestEntry.Open();
        var json = new StreamReader(stream).ReadToEnd();
        return JsonSerializer.Deserialize<ThemeManifest>(json)
            ?? throw new InvalidOperationException("Failed to parse manifest");
    }
}

public record ThemeManifest
{
    public string? Label { get; init; }
    public Dictionary<string, string>? FolderNames { get; init; }
    public Dictionary<string, string>? FileExtensions { get; init; }
    public Dictionary<string, ThemeIconDef>? IconDefinitions { get; init; }
}

public record ThemeIconDef
{
    public string? IconPath { get; init; }
}
```

**Step 2: Themes page XAML**

```xml
<StackPanel Spacing="16" Padding="24">
    <TextBlock Text="Themes" Style="{StaticResource TitleTextBlockStyle}" />

    <InfoBar Title="How to import"
             Message="Download a .vsix icon theme from the VS Code Marketplace, then click Import Theme below."
             Severity="Informational" IsOpen="True" IsClosable="False" />

    <Button Content="Browse VS Code Marketplace" Click="OpenMarketplace" />
    <Button Content="Import Theme (.vsix / .zip)" Click="ImportTheme" Style="{StaticResource AccentButtonStyle}" />

    <Expander Header="Active Theme" IsExpanded="True">
        <StackPanel>
            <TextBlock x:Name="ActiveThemeLabel" Text="No theme applied" />
            <Button Content="Remove Theme" Click="RemoveTheme" />
        </StackPanel>
    </Expander>
</StackPanel>
```

```csharp
private void OpenMarketplace(object sender, RoutedEventArgs e)
{
    _ = Launcher.LaunchUriAsync(new Uri(
        "https://marketplace.visualstudio.com/search?target=VSCode&category=Themes&sortBy=Installs"));
}

private async void ImportTheme(object sender, RoutedEventArgs e)
{
    var picker = new FileOpenPicker();
    picker.FileTypeFilter.Add(".vsix");
    picker.FileTypeFilter.Add(".zip");
    // ... InitializeWithWindow, pick file, call VsixThemeImporter
}
```

**Step 3: Commit**

```bash
git add src/FolderOrganizer.Core/Themes/ src/FolderOrganizer.App/Views/ThemesPage*
git commit -m "feat(app): implement Themes page with vsix import and marketplace link"
```

---

## Task 13: App — Settings Page

**Files:**
- Modify: `src/FolderOrganizer.App/Views/SettingsPage.xaml` + `.cs`

```xml
<StackPanel Spacing="16" Padding="24">
    <TextBlock Text="Settings" Style="{StaticResource TitleTextBlockStyle}" />

    <Expander Header="Danger Zone" IsExpanded="False">
        <StackPanel Spacing="8">
            <Button Content="Clear All Customizations" Click="ClearAll" Foreground="Red" />
            <Button Content="Reset Tag Registry" Click="ResetTags" />
            <Button Content="Unregister Context Menu" Click="UnregisterShell" />
        </StackPanel>
    </Expander>

    <ToggleSwitch Header="Show Tags column in Explorer" x:Name="TagsColumnToggle"
                  Toggled="TagsColumnToggled" />

    <TextBlock Text="Version 1.0.0" Style="{StaticResource CaptionTextBlockStyle}" />
</StackPanel>
```

"Clear All Customizations" reads `TouchLog`, strips ADS from every recorded path, deletes log.
"Unregister Context Menu" runs `regsvr32 /u FolderOrganizer.Shell.dll` as elevated process.

**Commit:**

```bash
git add src/FolderOrganizer.App/Views/SettingsPage*
git commit -m "feat(app): implement Settings page with clear/reset/unregister actions"
```

---

## Task 14: Windows Property Handler (Tags Explorer Column)

**Files:**
- Create: `src/FolderOrganizer.Shell/PropertyHandler/FolderOrganizerPropertyHandler.cs`

This is an advanced COM component implementing `IPropertyStore` and `IInitializeWithFile`. It reads ADS and exposes `FolderOrganizer.Tags` as a string property to Explorer.

**Step 1: Register the property schema**

Create `src/FolderOrganizer.Shell/FolderOrganizer.propdesc`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<schema xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xmlns="http://schemas.microsoft.com/windows/2006/propertydescription"
        schemaVersion="1.0">
  <propertyDescriptionList publisher="FolderOrganizer" product="FolderOrganizer">
    <propertyDescription name="FolderOrganizer.Tags" formatID="{YOUR-GUID}" propID="1">
      <description>Tags assigned by FolderOrganizer</description>
      <searchInfo inInvertedIndex="true" isColumn="true" />
      <typeInfo type="String" multipleValues="true" isInnate="false" />
      <labelInfo label="Organizer Tags" invitationText="Add tags..." />
    </propertyDescription>
  </propertyDescriptionList>
</schema>
```

Register via: `PSCoInstall /s FolderOrganizer.propdesc` (done by installer)

**Step 2: Implement IPropertyStore**

```csharp
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("NEW-GUID-FOR-PROPERTY-HANDLER")]
public class FolderOrganizerPropertyHandler : IPropertyStore, IInitializeWithFile
{
    private string _filePath = string.Empty;
    private readonly AdsStorage _ads = new();

    public void Initialize(string pszFilePath, uint grfMode)
    {
        _filePath = pszFilePath;
    }

    public uint GetCount()
    {
        return 1; // Only FolderOrganizer.Tags
    }

    public PROPERTYKEY GetAt(uint iProp)
    {
        return TagsPropertyKey;
    }

    public void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv)
    {
        pv = new PROPVARIANT();
        try
        {
            var meta = _ads.ReadMetadata(_filePath);
            var tagNames = string.Join("; ", meta.Tags.Select(t => t.Name));
            pv = PROPVARIANT.FromString(tagNames);
        }
        catch { }
    }

    // SetValue, Commit: no-op (read-only from Explorer; writes go through the app)
    public void SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar) { }
    public void Commit() { }

    private static readonly PROPERTYKEY TagsPropertyKey = new PROPERTYKEY
    {
        fmtid = new Guid("YOUR-PROPERTY-GUID"),
        pid = 1
    };
}
```

**Step 3: Register property handler**

Add to `[ComRegisterFunction]`:
```csharp
// Register property handler for all file types
using var phKey = Registry.LocalMachine.CreateSubKey(
    @"SOFTWARE\Microsoft\Windows\CurrentVersion\PropertySystem\PropertyHandlers\*");
phKey?.SetValue("", $"{{{PropertyHandlerGuid}}}");
```

**Step 4: Commit**

```bash
git add src/FolderOrganizer.Shell/PropertyHandler/
git commit -m "feat(shell): add Windows Property Handler for Tags Explorer column"
```

---

## Task 15: Installer (WiX)

**Files:**
- Create: `src/FolderOrganizer.Installer/FolderOrganizer.Installer.wixproj`
- Create: `src/FolderOrganizer.Installer/Package.wxs`

**Step 1: Install WiX v4**

```powershell
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext
wix extension add WixToolset.Util.wixext
```

**Step 2: Create WiX project**

```powershell
dotnet new wix -n FolderOrganizer.Installer -o src/FolderOrganizer.Installer
dotnet sln add src/FolderOrganizer.Installer/FolderOrganizer.Installer.wixproj
```

**Step 3: Package.wxs structure**

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="Folder Organizer" Manufacturer="FolderOrganizer"
           Version="1.0.0" UpgradeCode="GENERATE-NEW-GUID">

    <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />

    <Feature Id="Core">
      <!-- App files -->
      <ComponentGroupRef Id="AppComponents" />
      <!-- Shell DLL -->
      <ComponentRef Id="ShellDll" />
    </Feature>

    <Component Id="ShellDll" Directory="INSTALLFOLDER">
      <File Id="ShellDllFile" Source="$(var.ShellOutput)\FolderOrganizer.Shell.dll"
            KeyPath="yes" />
      <!-- COM registration via self-registration -->
      <RegistryValue Root="HKLM" Key="SOFTWARE\Classes\CLSID\{YOUR-GUID}"
                     Name="" Value="FolderOrganizer Context Menu" Type="string" />
      <!-- ... full COM registration keys ... -->
    </Component>

    <!-- Custom action: regsvr32 on install, regsvr32 /u on uninstall -->
    <CustomAction Id="RegisterShell" ExeCommand='[SystemFolder]regsvr32.exe /s "[INSTALLFOLDER]FolderOrganizer.Shell.dll"'
                  Directory="SystemFolder" Execute="deferred" Impersonate="no" />
    <CustomAction Id="UnregisterShell" ExeCommand='[SystemFolder]regsvr32.exe /s /u "[INSTALLFOLDER]FolderOrganizer.Shell.dll"'
                  Directory="SystemFolder" Execute="deferred" Impersonate="no" Return="ignore" />

    <InstallExecuteSequence>
      <Custom Action="RegisterShell" After="InstallFiles">NOT Installed</Custom>
      <Custom Action="UnregisterShell" Before="RemoveFiles">Installed AND NOT UPGRADINGPRODUCTCODE</Custom>
    </InstallExecuteSequence>

    <ui:WixUI Id="WixUI_Minimal" />
  </Package>
</Wix>
```

**Step 4: Build installer**

```powershell
dotnet build src/FolderOrganizer.Installer/ -c Release
# Output: src/FolderOrganizer.Installer/bin/Release/FolderOrganizer.msi
```

**Step 5: Final commit**

```bash
git add src/FolderOrganizer.Installer/
git commit -m "feat(installer): add WiX MSI installer with COM registration and clean uninstall"
```

---

## Testing Checklist

Run these manual tests before considering the project complete:

- [ ] Install MSI → right-click folder → "Organize" appears in menu
- [ ] Click each of the 9 Chrome colors → folder icon changes in Explorer
- [ ] Click `+` → color picker opens → custom color applied → new circle appears in future right-clicks
- [ ] Add a custom JPEG icon preset → appears in menu → click it → folder gets that icon
- [ ] Add a tag with color → tag appears in Explorer "Organizer Tags" column
- [ ] Move a customized folder to another location on same drive → customization preserved (ADS travels)
- [ ] Copy to USB drive → customization NOT present (expected — ADS doesn't cross filesystems)
- [ ] Open FolderOrganizer app → Presets page shows all custom colors, icons, tags
- [ ] Import a `.vsix` icon theme → folder icons update
- [ ] Settings → Clear All Customizations → all folders revert
- [ ] Uninstall MSI → right-click no longer shows "Organize" → no registry leftovers
- [ ] Kill Explorer.exe during a metadata write → restart Explorer → folder not corrupted

---

## References

- [IContextMenu2 owner draw (MSDN)](https://docs.microsoft.com/en-us/windows/win32/shell/context-menu-handlers)
- [NTFS Alternate Data Streams](https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/a82e9105-2405-4e37-b2c3-28c773902d85)
- [WinUI 3 NavigationView](https://docs.microsoft.com/en-us/windows/apps/design/controls/navigationview)
- [Windows Property System](https://docs.microsoft.com/en-us/windows/win32/properties/building-property-handlers)
- [WiX v4 docs](https://wixtoolset.org/docs/)
- [VS Code Marketplace icon themes](https://marketplace.visualstudio.com/search?target=VSCode&category=Themes&sortBy=Installs)
