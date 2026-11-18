using System;
using System.Numerics;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class TerrainHeightFieldTests
{
    // 64 × 64 samples 2 m apart from (100, 200).
    private static readonly TerrainGrid _grid = new(64, 2, 100, 200, new MapRect(100, 200, 226, 326));

    [Fact]
    public void SamplesTheGroundAtEverySample()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => (x / 4) + (z / 8));

        Assert.Equal(64, field.Size);
        Assert.Equal((100 / 4.0) + (200 / 8.0), field[0, 0]);
        Assert.Equal((110 / 4.0) + (230 / 8.0), field[5, 15]);
        Assert.Equal(field[5, 15], field.Heights[(15 * 64) + 5]);
        Assert.Equal(((float)((100 / 4.0) + (200 / 8.0)), (float)((226 / 4.0) + (326 / 8.0))), field.GetRange());
    }

    [Fact]
    public void GivesTheSameHeightsOnEveryRun()
    {
        static double Ground(double x, double z) => Math.Sqrt((x * x) + (z * z)) / 7;
        TerrainHeightField first = TerrainHeightField.Sample(_grid, Ground);
        TerrainHeightField second = TerrainHeightField.Sample(_grid, Ground);

        Assert.True(first.Heights.SequenceEqual(second.Heights));
    }

    [Fact]
    public void MeasuresSlopesOverTheBaseline()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => (0.5 * x) + (0.25 * z));

        Assert.Equal(Math.Sqrt((0.5 * 0.5) + (0.25 * 0.25)), field.SlopeAt(30, 30, 4), 6);
        Assert.Equal(Math.Sqrt((0.5 * 0.5) + (0.25 * 0.25)), field.SlopeAt(0, 63, 4), 6);
        Assert.Equal(0, TerrainHeightField.Sample(_grid, (x, z) => 3).SlopeAt(10, 10, 4));
    }

    [Fact]
    public void AppliesEditsInsideTheirBoundsOnly()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => 50);
        var layer = new TerrainEditLayer();
        layer.Add(new FootprintStamp(Square(120, 220, 130, 230, 40), StampMode.Set, falloffMeters: 0));

        long changed = field.Apply(layer);

        // Samples 10 to 15 along each axis lie on or inside the square: 6 × 6.
        Assert.Equal(36, changed);
        Assert.Equal(40, field[10, 10]);
        Assert.Equal(40, field[15, 15]);
        Assert.Equal(50, field[9, 10]);
        Assert.Equal(50, field[16, 16]);
    }

    [Fact]
    public void AppliesStampsInOrder()
    {
        TerrainHeightField field = TerrainHeightField.Sample(_grid, (x, z) => 50);
        var layer = new TerrainEditLayer();
        layer.Add(new FootprintStamp(Square(120, 220, 130, 230, 40), StampMode.Set, falloffMeters: 0));
        layer.Add(new FootprintStamp(Square(120, 220, 130, 230, 45), StampMode.Raise, falloffMeters: 0));

        field.Apply(layer);

        Assert.Equal(45, field[12, 12]);
        Assert.Equal(2, layer.Stamps.Count);
    }

    internal static Vector3[] Square(float minX, float minZ, float maxX, float maxZ, float height) =>
    [
        new(minX, height, minZ), new(maxX, height, minZ), new(maxX, height, maxZ),
        new(minX, height, minZ), new(maxX, height, maxZ), new(minX, height, maxZ),
    ];
}
