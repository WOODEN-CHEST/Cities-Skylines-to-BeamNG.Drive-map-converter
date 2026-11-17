using System.Collections.Generic;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>Facts about the exported city.</summary>
public sealed class CityInfo
{
    /// <summary>The city's name. Required; it may be empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The name of the map the city was started on, if known.</summary>
    public string? MapName { get; set; }

    /// <summary>The environment (climate) as the game names it, such as <c>Sunny</c>, if known.</summary>
    public string? Environment { get; set; }

    /// <summary>The name of the custom map theme the city uses, if any.</summary>
    public string? MapTheme { get; set; }

    /// <summary>Whether traffic drives on the left.</summary>
    public bool LeftHandTraffic { get; set; }

    /// <summary>The map tiles the city has unlocked.</summary>
    public IList<TileCoordinate> UnlockedTiles { get; } = new List<TileCoordinate>();
}
