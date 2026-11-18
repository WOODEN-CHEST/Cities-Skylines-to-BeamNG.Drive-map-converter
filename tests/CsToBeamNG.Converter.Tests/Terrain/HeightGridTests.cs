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
    public void InterpolatesSmoothlyThroughThePoints()
    {
        Assert.Equal(0, _grid.SampleSmooth(100, 200));
        Assert.Equal(11, _grid.SampleSmooth(110, 210), 12);
        Assert.Equal(102, _grid.SampleSmooth(120, 220));
        Assert.Equal(1, _grid.SampleSmooth(110, 200), 12);

        // Rows rise linearly along X, so along a row the cubic is the straight line.
        Assert.Equal(0.5, _grid.SampleSmooth(105, 200), 12);
    }

    [Fact]
    public void SmoothSamplesStayWithinTheSurroundingPoints()
    {
        for (double x = 100; x <= 120; x += 1.25)
        {
            for (double z = 200; z <= 220; z += 1.25)
            {
                int i = Math.Min((int)((x - 100) / 10), 1);
                int j = Math.Min((int)((z - 200) / 10), 1);
                double[] corners = [_grid.MetersAt(i, j), _grid.MetersAt(i + 1, j), _grid.MetersAt(i, j + 1), _grid.MetersAt(i + 1, j + 1)];
                Assert.InRange(_grid.SampleSmooth(x, z), Min(corners) - 1e-9, Max(corners) + 1e-9);
            }
        }
    }

    [Fact]
    public void SmoothSamplesFollowAPlaneExactly()
    {
        var plane = new HeightGrid(new GridGeometry(0, 0, 16, 5), PlaneUnits(5, (i, j) => 640 + (64 * i) + (128 * j)));

        Assert.Equal(plane.Sample(23.5, 41.25), plane.SampleSmooth(23.5, 41.25), 12);
        Assert.Equal(plane.Sample(3, 60), plane.SampleSmooth(3, 60), 12);
        Assert.Equal(plane.Sample(-50, 99), plane.SampleSmooth(-50, 99), 12);
    }

    [Fact]
    public void SmoothSamplesUseTheNearestEdgeOutsideTheGrid()
    {
        Assert.Equal(0, _grid.SampleSmooth(-1e6, -1e6));
        Assert.Equal(102, _grid.SampleSmooth(1e6, 1e6));
        Assert.Equal(0, _grid.SampleSmooth(double.NaN, double.NaN));
    }

    [Fact]
    public void RejectsHeightsThatDontFillTheGrid() =>
        Assert.Throws<ArgumentException>(() => new HeightGrid(new GridGeometry(0, 0, 1, 2), new ushort[3]));

    private static ushort[] PlaneUnits(int resolution, Func<int, int, int> units)
    {
        ushort[] values = new ushort[resolution * resolution];
        for (int j = 0; j < resolution; j++)
        {
            for (int i = 0; i < resolution; i++)
            {
                values[(j * resolution) + i] = (ushort)units(i, j);
            }
        }

        return values;
    }

    private static double Min(double[] values) => Math.Min(Math.Min(values[0], values[1]), Math.Min(values[2], values[3]));

    private static double Max(double[] values) => Math.Max(Math.Max(values[0], values[1]), Math.Max(values[2], values[3]));
}
