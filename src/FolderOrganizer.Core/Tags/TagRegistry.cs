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
