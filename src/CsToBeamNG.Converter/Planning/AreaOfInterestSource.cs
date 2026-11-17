namespace CsToBeamNG.Converter.Planning;

/// <summary>Where an <see cref="AreaOfInterest"/> came from.</summary>
public enum AreaOfInterestSource
{
    /// <summary>Tiles the user chose (<c>--tiles</c>), which override everything else.</summary>
    Tiles,

    /// <summary>The bounds of the sections' content plus a margin, clamped to the map.</summary>
    Content,

    /// <summary>The city's unlocked tiles, because no section gave content bounds.</summary>
    UnlockedTiles,

    /// <summary>The whole map, because neither content bounds nor unlocked tiles are known.</summary>
    Map,
}
