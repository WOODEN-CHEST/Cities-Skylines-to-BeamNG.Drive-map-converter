using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Converter.Planning;

/// <summary>Options for <see cref="AreaOfInterest.Plan"/>.</summary>
public sealed record AreaOfInterestOptions
{
    /// <summary>
    /// The default margin around the content, in metres: enough open ground that the level doesn't end at the last
    /// building, without inflating the terrain much. A judgment call; the terrain step may tune it.
    /// </summary>
    public const float DefaultMarginMeters = 500;

    /// <summary>The defaults: content bounds plus <see cref="DefaultMarginMeters"/>.</summary>
    public static AreaOfInterestOptions Default { get; } = new();

    /// <summary>The margin added around the content bounds, in metres, 0 or more.</summary>
    public float MarginMeters { get; init; } = DefaultMarginMeters;

    /// <summary>
    /// Tiles that override the computed area: the area becomes the smallest rectangle covering them, without a
    /// margin. <see langword="null"/> or empty to compute the area.
    /// </summary>
    public IReadOnlyCollection<TileCoordinate>? Tiles { get; init; }
}
