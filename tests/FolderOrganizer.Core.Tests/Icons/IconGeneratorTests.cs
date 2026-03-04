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
