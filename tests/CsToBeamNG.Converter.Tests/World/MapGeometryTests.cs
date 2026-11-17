using System.Collections.Generic;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.World;

public sealed class MapGeometryTests
{
    [Fact]
    public void HasTheMapSizeFromTheResearch()
    {
        // docs/research/cs1-save-and-world.md, World geometry: 9 × 9 tiles of 1920 m, ±8640 m.
        Assert.Equal(1920f, MapGeometry.TileSizeMeters);
        Assert.Equal(9, MapGeometry.TilesPerSide);
        Assert.Equal(17280f, MapGeometry.SizeMeters);
        Assert.Equal(8640f, MapGeometry.HalfSizeMeters);
        Assert.Equal(new MapRect(-8640, -8640, 8640, 8640), MapGeometry.Bounds);
    }

    [Theory]
    [InlineData(0, 0, -8640, -8640)]
    [InlineData(4, 4, -960, -960)]
    [InlineData(8, 8, 6720, 6720)]
    [InlineData(3, 4, -2880, -960)]
    [InlineData(4, 3, -960, -2880)]
    public void PlacesTilesLikeTheDump(int x, int z, float minX, float minZ) =>
        Assert.Equal(new MapRect(minX, minZ, minX + 1920, minZ + 1920), MapGeometry.TileBounds(new TileCoordinate(x, z)));

    [Fact]
    public void TilesCoverTheMapWithoutGaps()
    {
        var tiles = new List<TileCoordinate>();
        for (int z = 0; z < 9; z++)
        {
            for (int x = 0; x < 9; x++)
            {
                tiles.Add(new TileCoordinate(x, z));
                if (x > 0)
                {
                    Assert.Equal(MapGeometry.TileBounds(new TileCoordinate(x - 1, z)).MaxX, MapGeometry.TileBounds(new TileCoordinate(x, z)).MinX);
                }
            }
        }

        Assert.Equal(MapGeometry.Bounds, MapGeometry.TileBounds(tiles));
    }

    [Fact]
    public void BoundsSeveralTiles()
    {
        TileCoordinate[] tiles = [new(4, 3), new(3, 4), new(4, 4), new(4, 4)];

        Assert.Equal(new MapRect(-2880, -2880, 960, 960), MapGeometry.TileBounds(tiles));
        Assert.Null(MapGeometry.TileBounds([]));
    }
}
