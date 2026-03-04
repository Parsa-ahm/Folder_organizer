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
