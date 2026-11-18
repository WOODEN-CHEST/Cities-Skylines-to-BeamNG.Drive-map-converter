using System;
using System.Threading.Tasks;
using CsToBeamNG.Converter.Planning;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// The heights of a level's terrain at the samples of its <see cref="TerrainGrid"/>, in metres (the city's heights,
/// which are BeamNG's z), before they are stored in a <c>.ter</c> file. The height pipeline fills it from the city's
/// ground and then applies a <see cref="TerrainEditLayer"/> (docs/architecture.md, D37).
/// </summary>
public sealed class TerrainHeightField
{
    private readonly float[] _heights;

    private TerrainHeightField(TerrainGrid grid)
    {
        Grid = grid;
        _heights = new float[grid.Size * grid.Size];
    }

    /// <summary>The grid the heights belong to.</summary>
    public TerrainGrid Grid { get; }

    /// <summary>The samples per side.</summary>
    public int Size => Grid.Size;

    /// <summary>The heights, row by row in the grid's order (index = row × <see cref="Size"/> + column).</summary>
    public Span<float> Heights => _heights;

    /// <summary>The height of a sample, in metres.</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    public float this[int column, int row]
    {
        get => _heights[IndexOf(column, row)];
        set => _heights[IndexOf(column, row)] = value;
    }

    /// <summary>
    /// Creates the field by asking for the ground at every sample's city position. Rows are filled in parallel, so
    /// <paramref name="ground"/> must be safe to call from several threads; the result doesn't depend on the order.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="ground">The height at a city position (X, Z), in metres.</param>
    /// <returns>The field.</returns>
    public static TerrainHeightField Sample(TerrainGrid grid, Func<double, double, double> ground)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(ground);
        var field = new TerrainHeightField(grid);
        int size = grid.Size;
        float[] heights = field._heights;
        Parallel.For(0, size, row =>
        {
            int start = row * size;
            for (int column = 0; column < size; column++)
            {
                (double x, double z) = grid.ToCitiesSkylines(column, row);
                heights[start + column] = (float)ground(x, z);
            }
        });
        return field;
    }

    /// <summary>Applies the edits of a layer, stamp after stamp in the layer's order.</summary>
    /// <param name="layer">The edits.</param>
    /// <returns>The number of samples whose height changed.</returns>
    public long Apply(TerrainEditLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        long changed = 0;
        foreach (TerrainStamp stamp in layer.Stamps)
        {
            if (!Grid.TryGetSampleRange(stamp.Bounds, out int firstColumn, out int lastColumn, out int firstRow, out int lastRow))
            {
                continue;
            }

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    int index = (row * Size) + column;
                    (double x, double z) = Grid.ToCitiesSkylines(column, row);
                    float height = (float)stamp.Apply(x, z, _heights[index]);
                    if (height != _heights[index])
                    {
                        _heights[index] = height;
                        changed++;
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>Returns the lowest and highest height.</summary>
    public (float Lowest, float Highest) GetRange()
    {
        float lowest = float.MaxValue;
        float highest = float.MinValue;
        foreach (float height in _heights)
        {
            lowest = Math.Min(lowest, height);
            highest = Math.Max(highest, height);
        }

        return (lowest, highest);
    }

    /// <summary>
    /// Returns the steepness at a sample: the length of the height's gradient, rise over run, measured over about
    /// <paramref name="baselineMeters"/> on each side (fewer at the grid's edges).
    /// </summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    /// <param name="baselineMeters">How far to each side the slope is measured, at least one sample.</param>
    public double SlopeAt(int column, int row, double baselineMeters)
    {
        int reach = Math.Max(1, (int)Math.Round(baselineMeters / Grid.SquareSize));
        int left = Math.Max(column - reach, 0);
        int right = Math.Min(column + reach, Size - 1);
        int below = Math.Max(row - reach, 0);
        int above = Math.Min(row + reach, Size - 1);
        double s = Grid.SquareSize;
        double dx = right == left ? 0 : (this[right, row] - this[left, row]) / ((right - left) * s);
        double dy = above == below ? 0 : (this[column, above] - this[column, below]) / ((above - below) * s);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private int IndexOf(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)column, (uint)Size, nameof(column));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, (uint)Size, nameof(row));
        return (row * Size) + column;
    }
}
