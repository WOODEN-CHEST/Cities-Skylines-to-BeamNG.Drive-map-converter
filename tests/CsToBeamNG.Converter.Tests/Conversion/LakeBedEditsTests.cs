using CsToBeamNG.Converter.Conversion;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Conversion;

public sealed class LakeBedEditsTests
{
    [Fact]
    public void RaisesLakeBedsThatReachBelowTheSea()
    {
        var deep = new Lake(108, 40, 64, 16384, [new MapRect(0, 0, 32, 16), new MapRect(0, 16, 16, 32)]);
        var shallow = new Lake(108, 90, 16, 4096, [new MapRect(100, 100, 116, 116)]);

        TerrainEditLayer layer = LakeBedEdits.Create(Bodies(seaSurface: 60, deep, shallow));

        TerrainStamp stamp = Assert.Single(layer.Stamps);
        Assert.Equal(new MapRect(0, 0, 32, 32), stamp.Bounds);
        Assert.Equal(60.5, stamp.Apply(8, 8, 45), 4);
        Assert.Equal(60.5, stamp.Apply(8, 24, 59), 4);
        Assert.Equal(70, stamp.Apply(8, 8, 70), 4);

        // Outside the lake's boxes the terrain stays, even below the sea.
        Assert.Equal(45, stamp.Apply(24, 24, 45), 4);
    }

    [Fact]
    public void LeavesLakesAloneWithoutASea()
    {
        var deep = new Lake(108, 40, 64, 16384, [new MapRect(0, 0, 32, 16)]);

        Assert.Empty(LakeBedEdits.Create(Bodies(seaSurface: null, deep)).Stamps);
    }

    private static WaterBodies Bodies(double? seaSurface, params Lake[] lakes) => new()
    {
        SeaLevel = 60.3125,
        SeaSurface = seaSurface,
        SeaCells = 0,
        Lakes = lakes,
        SkippedBodies = 0,
        SkippedCells = 0,
        SkippedLowest = 0,
        SkippedHighest = 0,
        SmallWaterCells = 0,
        DryCellsBelowSea = 0,
        CellAreaSquareMeters = 256,
    };
}
