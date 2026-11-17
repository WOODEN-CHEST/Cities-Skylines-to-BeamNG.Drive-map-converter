using System;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Tests.Fixtures;

/// <summary>
/// Builds small terrains and water with the game's layout on a miniature map: 9 × 9 tiles of 16 m (−72 to 72 m), a
/// main grid 8 m apart (19 points) and detail tiles of 5 points 4 m apart (37 points across the map). Heights are given
/// as functions of the world position, in metres, and stored in 1/64 m like the game's.
/// </summary>
internal static class TestTerrain
{
    public const float HalfSize = 72;

    public const float TileSize = 16;

    public const int DetailTileResolution = 5;

    public static GridGeometry Grid { get; } = new(-HalfSize, -HalfSize, 8, 19);

    public static GridGeometry DetailGrid { get; } = new(-HalfSize, -HalfSize, 4, ((DetailTileResolution - 1) * TileCoordinate.GridSize) + 1);

    /// <summary>Returns the world X or Z of a tile's −X or −Z edge.</summary>
    public static float TileStart(int tile) => -HalfSize + (tile * TileSize);

    public static TerrainSectionContent Create(Func<double, double, double> raw, Func<double, double, double> final) =>
        new(Grid, Sample(Grid, raw), Sample(Grid, final), DetailGrid, DetailTileResolution);

    /// <summary>Adds a detail tile whose heights come from a function of the world position.</summary>
    public static TerrainSectionContent WithDetail(this TerrainSectionContent terrain, TileCoordinate tile, Func<double, double, double> heights)
    {
        ushort[] values = new ushort[DetailTileResolution * DetailTileResolution];
        for (int j = 0; j < DetailTileResolution; j++)
        {
            for (int i = 0; i < DetailTileResolution; i++)
            {
                double x = TileStart(tile.X) + (i * DetailGrid.Spacing);
                double z = TileStart(tile.Z) + (j * DetailGrid.Spacing);
                values[(j * DetailTileResolution) + i] = ToUnits(heights(x, z));
            }
        }

        terrain.DetailTiles.Add(new TerrainDetailTile(tile, values));
        return terrain;
    }

    /// <summary>Water on the main grid: the ground and the depth as functions of the world position, in metres.</summary>
    public static WaterSectionContent CreateWater(float seaLevel, Func<double, double, double> ground, Func<double, double, double> depth)
    {
        int points = Grid.PointCount;
        var water = new WaterSectionContent(seaLevel, Grid, Sample(Grid, ground), Sample(Grid, depth), new ushort[points], new short[points], new short[points]);
        return water;
    }

    public static ushort ToUnits(double meters) => (ushort)Math.Clamp(Math.Round(meters * 64), 0, ushort.MaxValue);

    private static ushort[] Sample(GridGeometry grid, Func<double, double, double> heights)
    {
        ushort[] values = new ushort[grid.PointCount];
        for (int j = 0; j < grid.Resolution; j++)
        {
            for (int i = 0; i < grid.Resolution; i++)
            {
                values[(j * grid.Resolution) + i] = ToUnits(heights(grid.OriginX + (i * grid.Spacing), grid.OriginZ + (j * grid.Spacing)));
            }
        }

        return values;
    }
}
