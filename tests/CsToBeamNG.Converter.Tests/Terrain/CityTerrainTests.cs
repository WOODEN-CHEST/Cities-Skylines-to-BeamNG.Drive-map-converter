using System;
using System.Linq;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class CityTerrainTests
{
    private const double Tolerance = 1e-9;

    // Tile (4, 4) spans x and z from −8 to 8 m on the miniature map; tile (5, 4) from 8 to 24 m.
    private static readonly TileCoordinate _centre = new(4, 4);

    [Fact]
    public void UsesTheFinalHeightsWhereATileHasNoDetail()
    {
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 1, (x, z) => 10 + (x * 0.25) + (z * 0.125)));

        Assert.Equal(10 + (13 * 0.25) + (-7 * 0.125), terrain.SampleGround(13, -7), Tolerance);
        Assert.Equal(terrain.FinalHeights.Sample(-50.3, 61.9), terrain.SampleGround(-50.3, 61.9));
        Assert.False(terrain.HasDetail(_centre));
    }

    [Fact]
    public void InterpolatesTheDetailHeightsInsideADetailTile()
    {
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 1, (x, z) => 50)
            .WithDetail(_centre, (x, z) => 20 + x + (2 * z)));

        Assert.True(terrain.HasDetail(_centre));
        Assert.Equal(20 + 1.5 + (2 * -3.25), terrain.SampleGround(1.5, -3.25), Tolerance);
        Assert.Equal(20 + 4 + (2 * 4), terrain.SampleGround(4, 4), Tolerance);
    }

    [Fact]
    public void UsesTheFinalHeightsOnTheNearEdgesNextToTilesWithoutDetail()
    {
        // The tile's −X and −Z edges, next to tiles without detail heights, take the final heights (0 here), so a point
        // 1 m inside lies a quarter of the way to the detail height of the next column.
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 0).WithDetail(_centre, (x, z) => 16));

        Assert.Equal(0, terrain.SampleGround(-8, 2), Tolerance);
        Assert.Equal(4, terrain.SampleGround(-7, 2), Tolerance);
        Assert.Equal(0, terrain.SampleGround(2, -8), Tolerance);
        Assert.Equal(4, terrain.SampleGround(2, -7), Tolerance);
        Assert.Equal(16, terrain.SampleGround(-4, 2), Tolerance);
    }

    [Fact]
    public void UsesTheNeighboursHeightsOnTheFarEdges()
    {
        // The +X edge line belongs to the tile beyond it: final heights where it has no detail, its own detail otherwise.
        var alone = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 0).WithDetail(_centre, (x, z) => 16));
        Assert.Equal(0, alone.SampleGround(8, 2), Tolerance);
        Assert.Equal(4, alone.SampleGround(7, 2), Tolerance);

        var pair = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 0)
            .WithDetail(_centre, (x, z) => 16)
            .WithDetail(new TileCoordinate(5, 4), (x, z) => 32));
        Assert.Equal(32, pair.SampleGround(8, 2), Tolerance);
        Assert.Equal(28, pair.SampleGround(7, 2), Tolerance);
        Assert.Equal(16, pair.SampleGround(4, 2), Tolerance);
    }

    [Fact]
    public void UsesTheLastTilesOwnEdgeAtTheMapsFarSide()
    {
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 0).WithDetail(new TileCoordinate(8, 8), (x, z) => 3 + (x / 72)));

        Assert.Equal(4, terrain.SampleGround(72, 70), Tolerance);
        Assert.Equal(4, terrain.SampleGround(500, 70), Tolerance);
    }

    [Fact]
    public void ReportsStatistics()
    {
        using var scope = new CultureScope("lv-LV");
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 10, (x, z) => x == -72 ? 12.5 : x == 72 ? 7 : 10)
            .WithDetail(new TileCoordinate(5, 4), (x, z) => 11)
            .WithDetail(_centre, (x, z) => 10.25));

        Assert.Equal(
            [
                ("grid", "19 × 19 points, 8 m apart, from (-72, -72)"),
                ("raw heights", "10 to 10 m"),
                ("final heights", "7 to 12.5 m"),
                ("levelled by roads and buildings", "38 points, raised up to 2.5 m, lowered up to 3 m"),
                ("detail heights", "2 tiles: (4, 4), (5, 4); 4 m apart, 10.25 to 11 m"),
            ],
            terrain.Statistics.Select(s => (s.Name, s.Value)));
        Assert.Null(terrain.AreaOfInterestBounds);
        Assert.Equal([_centre, new TileCoordinate(5, 4)], terrain.DetailTiles);
    }

    [Fact]
    public void SmoothsOnlyTheCoarseHeights()
    {
        // A bent final surface, so the smooth and the bilinear reading differ between the 8 m points.
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 20 + ((x * x) / 64))
            .WithDetail(_centre, (x, z) => 30 + x));

        // Away from detail tiles: the final heights, smoothed.
        Assert.Equal(terrain.FinalHeights.SampleSmooth(-30.5, 17), terrain.SampleGround(-30.5, 17, CoarseInterpolation.MonotoneCubic));
        Assert.NotEqual(terrain.SampleGround(-30.5, 17), terrain.SampleGround(-30.5, 17, CoarseInterpolation.MonotoneCubic), Tolerance);
        Assert.Equal(terrain.SampleGround(-30.5, 17), terrain.SampleGround(-30.5, 17, CoarseInterpolation.Bilinear));

        // Inside the detail tile, away from its edges next to tiles without detail: the detail heights, unchanged.
        Assert.Equal(terrain.SampleGround(1.5, 2.75), terrain.SampleGround(1.5, 2.75, CoarseInterpolation.MonotoneCubic));
        Assert.True(terrain.HasDetailAt(1.5, 2.75));
        Assert.False(terrain.HasDetailAt(-30.5, 17));
    }

    [Fact]
    public void SmoothCoarseHeightsMeetTheDetailTileWithoutAStep()
    {
        var terrain = new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => 20 + ((x * x) / 64) + (z / 4))
            .WithDetail(_centre, (x, z) => 30));

        // The tile's −X edge takes the final heights, smoothed, like the tile beside it.
        foreach (double z in new[] { -6.5, -1, 3.25 })
        {
            double outside = terrain.SampleGround(-8 - 1e-7, z, CoarseInterpolation.MonotoneCubic);
            double inside = terrain.SampleGround(-8 + 1e-7, z, CoarseInterpolation.MonotoneCubic);
            Assert.Equal(outside, inside, 1e-4);
        }
    }

    [Fact]
    public void RejectsDetailTilesThatDontSpanTheDetailGrid()
    {
        TerrainSectionContent content = TestTerrain.Create((x, z) => 0, (x, z) => 0);
        var odd = new TerrainSectionContent(content.Grid, content.RawHeights, content.FinalHeights, new GridGeometry(-72, -72, 4, 40), 5);

        Assert.Throws<ArgumentException>(() => new CityTerrain(odd));
    }
}
