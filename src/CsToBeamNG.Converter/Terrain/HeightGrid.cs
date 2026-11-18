using System;
using System.Globalization;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// A square grid of heights as the game stores them: 16-bit values in 1/64 m steps, at the points of a
/// <see cref="GridGeometry"/> (docs/dump-format.md, <c>terrain</c>). Values outside the grid are those of its edge,
/// as in the game's samplers.
/// </summary>
public sealed class HeightGrid
{
    /// <summary>The height of one stored unit, in metres (docs/research/cs1-save-and-world.md, Terrain).</summary>
    public const double MetersPerUnit = TerrainSection.MetersPerHeightUnit;

    private readonly ushort[] _heights;

    /// <summary>Creates a grid.</summary>
    /// <param name="geometry">Where the points lie.</param>
    /// <param name="heights">One height per point in 1/64 m, row by row. The grid keeps the array; don't change it.</param>
    /// <exception cref="ArgumentException">The number of heights doesn't match the geometry.</exception>
    public HeightGrid(GridGeometry geometry, ushort[] heights)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(heights);
        if (heights.Length != geometry.PointCount)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"{heights.Length} heights don't fill a grid of {geometry.PointCount} points."),
                nameof(heights));
        }

        Geometry = geometry;
        _heights = heights;
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        foreach (ushort height in heights)
        {
            min = Math.Min(min, height);
            max = Math.Max(max, height);
        }

        MinUnits = min;
        MaxUnits = max;
    }

    /// <summary>Where the points lie.</summary>
    public GridGeometry Geometry { get; }

    /// <summary>The number of points along each side.</summary>
    public int Resolution => Geometry.Resolution;

    /// <summary>The lowest stored height, in 1/64 m.</summary>
    public ushort MinUnits { get; }

    /// <summary>The highest stored height, in 1/64 m.</summary>
    public ushort MaxUnits { get; }

    /// <summary>The lowest height, in metres.</summary>
    public double MinMeters => MinUnits * MetersPerUnit;

    /// <summary>The highest height, in metres.</summary>
    public double MaxMeters => MaxUnits * MetersPerUnit;

    /// <summary>The heights in 1/64 m, row by row.</summary>
    public ReadOnlySpan<ushort> Units => _heights;

    /// <summary>Returns the stored height of a point, in 1/64 m.</summary>
    /// <param name="i">The point's column, along X.</param>
    /// <param name="j">The point's row, along Z.</param>
    /// <exception cref="ArgumentOutOfRangeException">The point is outside the grid.</exception>
    public ushort UnitsAt(int i, int j)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)i, (uint)Resolution, nameof(i));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)j, (uint)Resolution, nameof(j));
        return _heights[(j * Resolution) + i];
    }

    /// <summary>Returns the height of a point, in metres.</summary>
    /// <param name="i">The point's column, along X.</param>
    /// <param name="j">The point's row, along Z.</param>
    /// <exception cref="ArgumentOutOfRangeException">The point is outside the grid.</exception>
    public double MetersAt(int i, int j) => UnitsAt(i, j) * MetersPerUnit;

    /// <summary>
    /// Returns the height at a world position, in metres, interpolated bilinearly between the four surrounding points;
    /// outside the grid, the nearest edge applies. This is how the game's <c>TerrainManager.SampleFinalHeight</c> reads
    /// its grid (docs/research/cs1-save-and-world.md, Terrain).
    /// </summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double Sample(double x, double z) => SampleUnits(x, z) * MetersPerUnit;

    /// <summary>Returns the height at a world position like <see cref="Sample"/>, but in 1/64 m.</summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double SampleUnits(double x, double z) =>
        SampleGridUnits((x - Geometry.OriginX) / Geometry.Spacing, (z - Geometry.OriginZ) / Geometry.Spacing);

    /// <summary>
    /// Returns the bilinearly interpolated height at a position given in grid units (point (i, j) is at (i, j)), in
    /// 1/64 m; outside the grid, the nearest edge applies.
    /// </summary>
    /// <param name="gridX">The position along the columns.</param>
    /// <param name="gridZ">The position along the rows.</param>
    public double SampleGridUnits(double gridX, double gridZ)
    {
        int last = Resolution - 1;
        double x = Math.Clamp(double.IsNaN(gridX) ? 0 : gridX, 0, last);
        double z = Math.Clamp(double.IsNaN(gridZ) ? 0 : gridZ, 0, last);
        int i = Math.Min((int)x, last - 1);
        int j = Math.Min((int)z, last - 1);
        double tx = x - i;
        double tz = z - j;
        int row = j * Resolution;
        double bottom = Lerp(_heights[row + i], _heights[row + i + 1], tx);
        double top = Lerp(_heights[row + Resolution + i], _heights[row + Resolution + i + 1], tx);
        return Lerp(bottom, top, tz);
    }

    /// <summary>
    /// Returns the height at a world position, in metres, interpolated smoothly with <see cref="MonotoneCubic"/>
    /// between the 4 × 4 surrounding points, first along X, then along Z. It equals the stored heights at the points
    /// and stays within the range of the four points around the position; outside the grid, the nearest edge applies.
    /// </summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double SampleSmooth(double x, double z) => SampleSmoothUnits(x, z) * MetersPerUnit;

    /// <summary>Returns the height at a world position like <see cref="SampleSmooth"/>, but in 1/64 m.</summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double SampleSmoothUnits(double x, double z) =>
        SampleSmoothGridUnits((x - Geometry.OriginX) / Geometry.Spacing, (z - Geometry.OriginZ) / Geometry.Spacing);

    /// <summary>
    /// Returns the smoothly interpolated height (<see cref="SampleSmooth"/>) at a position given in grid units, in
    /// 1/64 m; outside the grid, the nearest edge applies.
    /// </summary>
    /// <param name="gridX">The position along the columns.</param>
    /// <param name="gridZ">The position along the rows.</param>
    public double SampleSmoothGridUnits(double gridX, double gridZ)
    {
        int last = Resolution - 1;
        double x = Math.Clamp(double.IsNaN(gridX) ? 0 : gridX, 0, last);
        double z = Math.Clamp(double.IsNaN(gridZ) ? 0 : gridZ, 0, last);
        int i = Math.Min((int)x, last - 1);
        int j = Math.Min((int)z, last - 1);
        double tx = x - i;
        double tz = z - j;
        double row1 = SmoothRow(j, i, tx);
        double row2 = SmoothRow(j + 1, i, tx);
        double row0 = j > 0 ? SmoothRow(j - 1, i, tx) : (2 * row1) - row2;
        double row3 = j + 2 <= last ? SmoothRow(j + 2, i, tx) : (2 * row2) - row1;
        return MonotoneCubic.Interpolate(row0, row1, row2, row3, tz);
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    // The smooth interpolation along row j between columns i and i + 1. Beyond the grid's edge the missing neighbour
    // continues the edge interval in a straight line, so straight slopes stay straight up to the edge.
    private double SmoothRow(int j, int i, double tx)
    {
        int row = j * Resolution;
        double p1 = _heights[row + i];
        double p2 = _heights[row + i + 1];
        double p0 = i > 0 ? _heights[row + i - 1] : (2 * p1) - p2;
        double p3 = i + 2 < Resolution ? _heights[row + i + 2] : (2 * p2) - p1;
        return MonotoneCubic.Interpolate(p0, p1, p2, p3, tx);
    }
}
