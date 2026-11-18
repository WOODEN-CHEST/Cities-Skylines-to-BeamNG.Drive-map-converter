using System.Collections.Generic;

namespace CsToBeamNG.Converter.Water;

/// <summary>
/// The city's water within an area, sorted into the sea, flat lakes and the rest, which the level leaves out for now:
/// rivers and other sloping water, and water too small to be a lake (docs/architecture.md, D39).
/// </summary>
public sealed class WaterBodies
{
    /// <summary>The game's sea level, in metres.</summary>
    public required double SeaLevel { get; init; }

    /// <summary>
    /// Where the game draws the sea, in metres (the sea level less the drawn offset), or <see langword="null"/> if
    /// the map has no sea: no water at the sea level along its edges.
    /// </summary>
    public required double? SeaSurface { get; init; }

    /// <summary>The number of the sea's cells within the area.</summary>
    public required int SeaCells { get; init; }

    /// <summary>The lakes within the area, in the order they were found.</summary>
    public required IReadOnlyList<Lake> Lakes { get; init; }

    /// <summary>
    /// The number of bodies of water left out that have at least <see cref="WaterBodyOptions.MinLakeCells"/> cells:
    /// rivers and other sloping water.
    /// </summary>
    public required int SkippedBodies { get; init; }

    /// <summary>The number of cells of those bodies.</summary>
    public required int SkippedCells { get; init; }

    /// <summary>The lowest drawn surface of those bodies, in metres, or 0 without any.</summary>
    public required double SkippedLowest { get; init; }

    /// <summary>The highest drawn surface of those bodies, in metres, or 0 without any.</summary>
    public required double SkippedHighest { get; init; }

    /// <summary>
    /// The number of cells of water left out because their bodies are too small for lakes: ponds, and cells that the
    /// sea's waves lift out of its tolerance (which the sea covers anyway).
    /// </summary>
    public required int SmallWaterCells { get; init; }

    /// <summary>
    /// The number of dry cells within the area whose ground lies below the sea's surface, which a sea plane floods. In
    /// the test cities they lie under buildings that stand in the water, which the game treats as obstacles.
    /// </summary>
    public required int DryCellsBelowSea { get; init; }

    /// <summary>The area of one cell, in square metres.</summary>
    public required double CellAreaSquareMeters { get; init; }
}
