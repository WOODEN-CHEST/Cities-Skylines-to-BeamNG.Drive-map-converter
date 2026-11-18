using System.Linq;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Water;

public sealed class NearbyWaterTests
{
    [Fact]
    public void FindsTheDrawnSurfaceNearWater()
    {
        // 10 × 10 cells 16 m apart from (0, 0); one wet cell at (5, 5): ground 40 m, 3 m deep.
        var grid = new GridGeometry(0, 0, 16, 10);
        ushort[] ground = [.. Enumerable.Repeat((ushort)(45 * 64), 100)];
        ushort[] depth = new ushort[100];
        ground[55] = 40 * 64;
        depth[55] = 3 * 64;
        var water = new CityWater(new WaterSectionContent(40, grid, ground, depth, new ushort[100], new short[100], new short[100]), []);

        var nearby = new NearbyWater(water);

        double drawn = 43 - (CityWater.DrawnOffsetUnits * CityWater.MetersPerUnit);
        Assert.Equal(drawn, water.DrawnSurfaceAt(5, 5)!.Value, 9);
        Assert.Equal(drawn, nearby.SurfaceNear(80, 80), 6);
        Assert.Equal(drawn, nearby.SurfaceNear(100, 70), 6);
        Assert.True(double.IsNaN(nearby.SurfaceNear(10, 10)));
        Assert.True(double.IsNaN(nearby.SurfaceNear(140, 140)));
        Assert.Null(water.DrawnSurfaceAt(4, 5));
    }
}
