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
