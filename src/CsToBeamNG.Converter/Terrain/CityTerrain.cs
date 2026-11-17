using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// The city's terrain in the world model: the raw and final heights on the 16 m grid and the 4 m detail heights of
/// the tiles that have them, in the game's units (docs/dump-format.md, <c>terrain</c>).
/// </summary>
/// <remarks>
/// The ground the game draws, and places objects on, is the final heights, refined by the detail heights where a tile
/// has them; <see cref="SampleGround"/> reads it the way the game's <c>TerrainManager.SampleDetailHeight</c> does
/// (docs/research/cs1-save-and-world.md, Terrain).
/// </remarks>
public sealed class CityTerrain : ISectionData
{
    private const int TilesPerSide = TileCoordinate.GridSize;

    // The detail heights of each tile, indexed by z · 9 + x, or null where the tile has none.
    private readonly ushort[]?[] _detailTiles = new ushort[]?[TilesPerSide * TilesPerSide];

    /// <summary>Creates the terrain from a section's content.</summary>
    /// <param name="content">The content, as <see cref="TerrainSectionReader"/> read it. The terrain keeps its arrays.</param>
    /// <exception cref="ArgumentException">The content is inconsistent.</exception>
    public CityTerrain(TerrainSectionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RawHeights = new HeightGrid(content.Grid, content.RawHeights);
        FinalHeights = new HeightGrid(content.Grid, content.FinalHeights);
        DetailGrid = content.DetailGrid;
        DetailTileResolution = content.DetailTileResolution;
        if (((DetailTileResolution - 1) * TilesPerSide) + 1 != DetailGrid.Resolution)
        {
            throw new ArgumentException("The detail tiles don't span the detail grid.", nameof(content));
        }

        var tiles = new List<TileCoordinate>();
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        foreach (TerrainDetailTile tile in content.DetailTiles)
        {
            if (tile.Heights.Length != DetailTileResolution * DetailTileResolution)
            {
                throw new ArgumentException("Tile " + tile.Tile + " has the wrong number of detail heights.", nameof(content));
            }

            _detailTiles[(tile.Tile.Z * TilesPerSide) + tile.Tile.X] = tile.Heights;
            tiles.Add(tile.Tile);
            foreach (ushort height in tile.Heights)
            {
                min = Math.Min(min, height);
                max = Math.Max(max, height);
            }
        }

        DetailTiles = [.. tiles.OrderBy(tile => tile.Z).ThenBy(tile => tile.X)];
        DetailMinUnits = tiles.Count == 0 ? (ushort)0 : min;
        DetailMaxUnits = tiles.Count == 0 ? (ushort)0 : max;
        LevelledPoints = CountDifferences(content.FinalHeights, content.RawHeights, out double raisedMeters, out double loweredMeters);
        MaxRaisedMeters = raisedMeters;
        MaxLoweredMeters = loweredMeters;
    }

    /// <summary>The raw heights: the terrain as the map and the landscaping tools shaped it.</summary>
    public HeightGrid RawHeights { get; }

    /// <summary>The final heights: the raw heights after roads and buildings levelled the ground.</summary>
    public HeightGrid FinalHeights { get; }

    /// <summary>The 4 m lattice that the detail heights of all tiles share.</summary>
    public GridGeometry DetailGrid { get; }

    /// <summary>The number of detail points along each side of a tile, its edges included.</summary>
    public int DetailTileResolution { get; }

    /// <summary>The tiles that have detail heights, ordered by z, then x.</summary>
    public IReadOnlyList<TileCoordinate> DetailTiles { get; }

    /// <summary>The lowest detail height in 1/64 m, or 0 without detail tiles.</summary>
    public ushort DetailMinUnits { get; }

    /// <summary>The highest detail height in 1/64 m, or 0 without detail tiles.</summary>
    public ushort DetailMaxUnits { get; }

    /// <summary>The number of grid points where the final height differs from the raw height.</summary>
    public int LevelledPoints { get; }

    /// <summary>The most that roads and buildings raised the ground above the raw height, in metres.</summary>
    public double MaxRaisedMeters { get; }

    /// <summary>The most that roads and buildings lowered the ground below the raw height, in metres (0 or more).</summary>
    public double MaxLoweredMeters { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("grid", FinalHeights.Geometry.ToString()),
        new("raw heights", HeightRange(RawHeights.MinMeters, RawHeights.MaxMeters)),
        new("final heights", HeightRange(FinalHeights.MinMeters, FinalHeights.MaxMeters)),
        new("levelled by roads and buildings", string.Create(
            CultureInfo.InvariantCulture,
            $"{LevelledPoints} points, raised up to {MaxRaisedMeters:0.##} m, lowered up to {MaxLoweredMeters:0.##} m")),
        new("detail heights", DetailTiles.Count == 0
            ? "none"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{DetailTiles.Count} {(DetailTiles.Count == 1 ? "tile" : "tiles")}: {string.Join(", ", DetailTiles)}; {DetailGrid.Spacing:0.##} m apart, {HeightRange(DetailMinUnits * HeightGrid.MetersPerUnit, DetailMaxUnits * HeightGrid.MetersPerUnit)}")),
    ];

    /// <inheritdoc/>
    /// <remarks>The terrain covers the whole map, so it doesn't bound the area of interest.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Whether a tile has detail heights.</summary>
    /// <param name="tile">The tile.</param>
    public bool HasDetail(TileCoordinate tile) => _detailTiles[(tile.Z * TilesPerSide) + tile.X] is not null;

    /// <summary>
    /// Returns the height of the ground the game draws at a world position, in metres: the detail heights,
    /// interpolated bilinearly, where the tile has them, and the final heights elsewhere. It follows the game's
    /// <c>TerrainManager.SampleDetailHeight</c>, including its rule for the edges of a detail tile next to a tile
    /// without (docs/research/cs1-save-and-world.md, Terrain).
    /// </summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double SampleGround(double x, double z)
    {
        int last = DetailGrid.Resolution - 1;
        double gridX = Math.Clamp(Finite((x - DetailGrid.OriginX) / DetailGrid.Spacing), 0, last);
        double gridZ = Math.Clamp(Finite((z - DetailGrid.OriginZ) / DetailGrid.Spacing), 0, last);
        int i = (int)gridX;
        int j = (int)gridZ;
        if (DetailTileAt(i, j) is null)
        {
            return FinalHeights.Sample(x, z);
        }

        int i1 = Math.Min(i + 1, last);
        int j1 = Math.Min(j + 1, last);
        double tx = gridX - i;
        double tz = gridZ - j;
        double bottom = Lerp(DetailUnits(i, j), DetailUnits(i1, j), tx);
        double top = Lerp(DetailUnits(i, j1), DetailUnits(i1, j1), tx);
        return Lerp(bottom, top, tz) * HeightGrid.MetersPerUnit;
    }

    private static double Finite(double value) => double.IsNaN(value) ? 0 : value;

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static string HeightRange(double min, double max) =>
        string.Create(CultureInfo.InvariantCulture, $"{min:0.##} to {max:0.##} m");

    private static int CountDifferences(ushort[] final, ushort[] raw, out double raisedMeters, out double loweredMeters)
    {
        int count = 0;
        int raised = 0;
        int lowered = 0;
        for (int k = 0; k < final.Length; k++)
        {
            int difference = final[k] - raw[k];
            if (difference != 0)
            {
                count++;
                raised = Math.Max(raised, difference);
                lowered = Math.Max(lowered, -difference);
            }
        }

        raisedMeters = raised * HeightGrid.MetersPerUnit;
        loweredMeters = lowered * HeightGrid.MetersPerUnit;
        return count;
    }

    // The detail heights of the tile that holds detail point (i, j); a point on the line between two tiles belongs to
    // the tile on its +X or +Z side, except on the map's far edges.
    private ushort[]? DetailTileAt(int i, int j)
    {
        int cells = DetailTileResolution - 1;
        int tileX = Math.Min(i / cells, TilesPerSide - 1);
        int tileZ = Math.Min(j / cells, TilesPerSide - 1);
        return _detailTiles[(tileZ * TilesPerSide) + tileX];
    }

    // The height of detail point (i, j) in 1/64 m, as the game's TerrainManager.GetDetailHeight gives it: from the
    // tile's detail heights, or from the final heights where the tile has none. On a tile's −X or −Z edge next to a
    // tile without detail heights, the final heights apply too, so neighbouring tiles meet without a step.
    private double DetailUnits(int i, int j)
    {
        int cells = DetailTileResolution - 1;
        int tileX = Math.Min(i / cells, TilesPerSide - 1);
        int tileZ = Math.Min(j / cells, TilesPerSide - 1);
        ushort[]? heights = _detailTiles[(tileZ * TilesPerSide) + tileX];
        int localX = i - (tileX * cells);
        int localZ = j - (tileZ * cells);
        bool finalEdge = heights is null
            || (localX == 0 && tileX > 0 && _detailTiles[(tileZ * TilesPerSide) + tileX - 1] is null)
            || (localZ == 0 && tileZ > 0 && _detailTiles[((tileZ - 1) * TilesPerSide) + tileX] is null);
        if (finalEdge)
        {
            double x = DetailGrid.OriginX + ((double)i * DetailGrid.Spacing);
            double z = DetailGrid.OriginZ + ((double)j * DetailGrid.Spacing);
            return FinalHeights.SampleUnits(x, z);
        }

        return heights![(localZ * DetailTileResolution) + localX];
    }
}
