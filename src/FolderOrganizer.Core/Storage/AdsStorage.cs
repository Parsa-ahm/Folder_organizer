using System;
using System.IO;
using System.Text;
using FolderOrganizer.Core.Models;

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
