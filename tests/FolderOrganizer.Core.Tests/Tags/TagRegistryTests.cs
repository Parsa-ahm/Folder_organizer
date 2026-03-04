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
