using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class SectionSummaryTests
{
    private static readonly GridGeometry _grid = new(-8, -8, 8, 3);

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void DescribesATerrain(string culture)
    {
        using var scope = new CultureScope(culture);
        var terrain = new TerrainSectionContent(
            _grid,
            [0, 64, 128, 192, 256, 320, 384, 448, 40000],
            [32, 64, 128, 192, 256, 320, 384, 448, 40001],
            new GridGeometry(-8, -8, 4, 19),
            3);
        terrain.DetailTiles.Add(new TerrainDetailTile(new TileCoordinate(4, 3), new ushort[9]));
        terrain.DetailTiles.Add(new TerrainDetailTile(new TileCoordinate(3, 4), new ushort[9]));

        Assert.Equal(
            "3 × 3 heights, raw 0 to 625 m, final 0.5 to 625.016 m; detail heights for tiles (4, 3), (3, 4)",
            SectionSummary.Describe(terrain));
    }

    [Fact]
    public void DescribesATerrainWithoutDetailTiles()
    {
        var terrain = new TerrainSectionContent(_grid, new ushort[9], new ushort[9], new GridGeometry(-8, -8, 4, 19), 3);

        Assert.EndsWith("; detail heights for no tiles", SectionSummary.Describe(terrain));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void DescribesTheWater(string culture)
    {
        using var scope = new CultureScope(culture);
        var water = new WaterSectionContent(59.999004f, _grid, new ushort[9], [0, 0, 5, 0, 7, 0, 0, 0, 1], new ushort[9], new short[9], new short[9]);
        water.Sources.Add(new WaterSourceRecord { Id = 1 });

        Assert.Equal("sea level 59.999 m; 3 of 9 cells hold water; 1 water sources", SectionSummary.Describe(water));
    }
}
