using CsToBeamNG.Converter.Conversion;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Conversion;

public sealed class SpawnPlacementTests
{
    private static readonly TerrainGrid _grid = new(128, 1, 0, 0, new MapRect(0, 0, 127, 127));

    [Fact]
    public void TakesTheWantedSampleIfItIsSuitable()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => 50);

        Assert.Equal((64, 64), SpawnPlacement.FindNear(field, 64, 64, (c, r) => false));
    }

    [Fact]
    public void FindsTheNearestDryLevelGround()
    {
        // Water west of x = 80, and a steep bank from 80 to 90.
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => x < 90 ? 10 + System.Math.Max(0, x - 80) : 20);

        (int Column, int Row)? found = SpawnPlacement.FindNear(field, 64, 64, (c, r) => c < 80);

        Assert.NotNull(found);
        Assert.InRange(found.Value.Column, 92, 100);
        Assert.InRange(found.Value.Row, 30, 98);
    }

    [Fact]
    public void FindsNothingWhereAllIsWater()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => 0);

        Assert.Null(SpawnPlacement.FindNear(field, 10, 10, (c, r) => true));
    }
}
