using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class TerrainSurfaceRulesTests
{
    private static readonly TerrainSurfaceRules _rules = TerrainSurfaceRules.Default;

    [Theory]
    [InlineData(0.1, 100, double.NaN, SurfaceKind.Grass)]
    [InlineData(0.5, 100, double.NaN, SurfaceKind.Dirt)]
    [InlineData(0.8, 100, double.NaN, SurfaceKind.Rock)]
    [InlineData(0.1, 100, 99, SurfaceKind.Sand)]
    [InlineData(0.1, 95, 99, SurfaceKind.Sand)]
    [InlineData(0.1, 101, 99, SurfaceKind.Grass)]
    [InlineData(0.5, 99.5, 99, SurfaceKind.Sand)]
    [InlineData(0.8, 99.5, 99, SurfaceKind.Rock)]
    public void ClassifiesBySlopeAndWater(double slope, double height, double water, SurfaceKind expected) =>
        Assert.Equal(expected, _rules.Classify(slope, height, water));

    [Fact]
    public void PaintsATerrainByItsSlopes()
    {
        // Flat at 100 m up to x = 20, a 45° slope up to x = 40, then flat again.
        var grid = new TerrainGrid(64, 1, 0, 0, new MapRect(0, 0, 63, 63));
        TerrainHeightField field = TerrainHeightField.Sample(grid, (x, z) => 100 + System.Math.Clamp(x - 20, 0, 20));

        SurfaceKind[] kinds = _rules.Classify(field, water: null);

        Assert.Equal(SurfaceKind.Grass, kinds[(30 * 64) + 5]);
        Assert.Equal(SurfaceKind.Rock, kinds[(30 * 64) + 30]);
        Assert.Equal(SurfaceKind.Grass, kinds[(30 * 64) + 55]);
    }
}
