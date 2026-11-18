namespace CsToBeamNG.Converter.Water;

/// <summary>
/// The thresholds <see cref="WaterBodyFinder"/> sorts the city's water with. The simulated surface is noisy: the game's
/// sea and lakes carry waves of a few metres between neighbouring cells, so the tolerances are wide.
/// </summary>
public sealed record WaterBodyOptions
{
    /// <summary>The defaults, tuned on CityPalma and CItyRockville (docs/research/cs1-save-and-world.md, Water).</summary>
    public static WaterBodyOptions Default { get; } = new();

    /// <summary>How far a cell's surface may lie from the sea level and still count as sea, in metres.</summary>
    public double SeaToleranceMeters { get; init; } = 3;

    /// <summary>The fewest cells a lake has: smaller flat water is left out with the rivers.</summary>
    public int MinLakeCells { get; init; } = 16;

    /// <summary>
    /// The fewest cells a flat part of a larger body of water has to count as a lake (5000 cells of 16 m are
    /// 1.28 km²). Rivers in the game run through level pools between steps; across both test maps the largest pool has
    /// 3100 cells, while the lakes that rivers flow through have 10,635 and 327,487. A pool shown as a lake would end in
    /// a sheet of water hanging over the dry river bed below it.
    /// </summary>
    public int MinAttachedLakeCells { get; init; } = 5000;

    /// <summary>
    /// How far below a lake's surface the ground of a dry cell next to it may lie for the lake's water to still cover it,
    /// in metres. Such cells are the lake's shallow fringe, where the game draws little or no water.
    /// </summary>
    public double ShoreToleranceMeters { get; init; } = 0.5;

    /// <summary>The width of the surface levels grouped to find a body's most common level, in metres.</summary>
    public double LevelBinMeters { get; init; } = 0.5;

    /// <summary>How far above or below that level a lake's cells may lie, in metres.</summary>
    public double LakeBandMeters { get; init; } = 1.5;

    /// <summary>
    /// The distance from a candidate lake's median level within which <see cref="LakeCoreFraction"/> of its cells must
    /// lie, in metres. A river's cells spread evenly over the whole band instead.
    /// </summary>
    public double LakeCoreMeters { get; init; } = 0.5;

    /// <summary>The share of a lake's cells that lie within <see cref="LakeCoreMeters"/> of its median level.</summary>
    public double LakeCoreFraction { get; init; } = 0.5;

    /// <summary>How far below the lowest ground around a lake its <c>WaterBlock</c> reaches, in metres.</summary>
    public double LakeBottomMarginMeters { get; init; } = 5;
}
