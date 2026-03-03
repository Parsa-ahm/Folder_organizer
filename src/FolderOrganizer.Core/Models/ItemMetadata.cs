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
            return JsonSerializer.Deserialize<ItemMetadata>(json!) ?? new ItemMetadata();
        }
        catch
        {
            return new ItemMetadata();
        }
    }
}
