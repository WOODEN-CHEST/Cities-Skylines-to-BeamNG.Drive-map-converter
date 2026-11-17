using System;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class HeightGridTests
{
    // 3 × 3 points 10 m apart from (100, 200), heights in 1/64 m.
    private static readonly HeightGrid _grid = new(
        new GridGeometry(100, 200, 10, 3),
        [
            0, 64, 128,
            640, 704, 768,
            6400, 6464, 6528,
        ]);

    [Fact]
    public void ReadsPointsInUnitsAndMetres()
    {
        Assert.Equal(704, _grid.UnitsAt(1, 1));
        Assert.Equal(12, _grid.MetersAt(2, 1));
        Assert.Equal(0, _grid.MinUnits);
        Assert.Equal(6528, _grid.MaxUnits);
        Assert.Equal(102, _grid.MaxMeters);
        Assert.Throws<ArgumentOutOfRangeException>(() => _grid.UnitsAt(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _grid.UnitsAt(0, -1));
    }

    [Fact]
    public void InterpolatesBilinearly()
    {
        Assert.Equal(0, _grid.Sample(100, 200));
        Assert.Equal(1, _grid.Sample(110, 200));
        Assert.Equal(0.5, _grid.Sample(105, 200));
        Assert.Equal(5.5, _grid.Sample(105, 205));
        Assert.Equal(56.5, _grid.Sample(115, 215), 12);
        Assert.Equal(102, _grid.Sample(120, 220));
    }

    [Fact]
    public void UsesTheNearestEdgeOutsideTheGrid()
    {
        Assert.Equal(0, _grid.Sample(-1e6, -1e6));
        Assert.Equal(102, _grid.Sample(1e6, 1e6));
        Assert.Equal(_grid.Sample(105, 220), _grid.Sample(105, 999));
        Assert.Equal(0, _grid.Sample(double.NaN, double.NaN));
    }

    [Fact]
    public void RejectsHeightsThatDontFillTheGrid() =>
        Assert.Throws<ArgumentException>(() => new HeightGrid(new GridGeometry(0, 0, 1, 2), new ushort[3]));
}
