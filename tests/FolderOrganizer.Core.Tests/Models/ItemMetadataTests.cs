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
