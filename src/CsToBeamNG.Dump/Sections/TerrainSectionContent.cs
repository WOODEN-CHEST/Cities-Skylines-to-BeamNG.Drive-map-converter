using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>terrain</c> section (docs/dump-format.md, <c>terrain</c>): what
/// <see cref="TerrainSectionWriter"/> writes and the .NET 10 build's <c>TerrainSectionReader</c> returns. Heights are
/// in 1/64 m (<see cref="TerrainSection.MetersPerHeightUnit"/>), as the game stores them.
/// </summary>
public sealed class TerrainSectionContent
{
    /// <summary>Creates the content.</summary>
    /// <param name="grid">Where the raw and final heights lie.</param>
    /// <param name="rawHeights">The raw heights, one per point of <paramref name="grid"/>, row by row.</param>
    /// <param name="finalHeights">The final heights, one per point of <paramref name="grid"/>, row by row.</param>
    /// <param name="detailGrid">
    /// The lattice that the detail heights of all tiles share; it spans the whole map, but only the tiles in
    /// <see cref="DetailTiles"/> have heights.
    /// </param>
    /// <param name="detailTileResolution">
    /// The number of detail points along each side of a tile, its edges included, so neighbouring tiles share their
    /// edge points: the lattice's resolution is 9 · (this − 1) + 1.
    /// </param>
    public TerrainSectionContent(GridGeometry grid, ushort[] rawHeights, ushort[] finalHeights, GridGeometry detailGrid, int detailTileResolution)
    {
        Guard.NotNull(grid, nameof(grid));
        Guard.NotNull(rawHeights, nameof(rawHeights));
        Guard.NotNull(finalHeights, nameof(finalHeights));
        Guard.NotNull(detailGrid, nameof(detailGrid));
        Grid = grid;
        RawHeights = rawHeights;
        FinalHeights = finalHeights;
        DetailGrid = detailGrid;
        DetailTileResolution = detailTileResolution;
    }

    /// <summary>Where the raw and final heights lie.</summary>
    public GridGeometry Grid { get; }

    /// <summary>The raw heights in 1/64 m, row by row: the terrain as the map and the landscaping tools shaped it.</summary>
    public ushort[] RawHeights { get; }

    /// <summary>The final heights in 1/64 m, row by row: the raw heights after roads and buildings have levelled the ground.</summary>
    public ushort[] FinalHeights { get; }

    /// <summary>The lattice that the detail heights of all tiles share.</summary>
    public GridGeometry DetailGrid { get; }

    /// <summary>The number of detail points along each side of a tile, its edges included.</summary>
    public int DetailTileResolution { get; }

    /// <summary>The tiles that have detail heights, ordered by z, then x.</summary>
    public IList<TerrainDetailTile> DetailTiles { get; } = new List<TerrainDetailTile>();
}
