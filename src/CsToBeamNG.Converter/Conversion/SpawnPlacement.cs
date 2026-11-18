using System;
using CsToBeamNG.Converter.Terrain;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>Finds dry, level ground for a spawn point near a wanted position on the terrain.</summary>
public static class SpawnPlacement
{
    /// <summary>The steepest slope a spawn point stands on, rise over run (about 8.5°).</summary>
    public const double MaxSlope = 0.15;

    /// <summary>How far apart the candidate samples are, in metres.</summary>
    public const double StepMeters = 4;

    /// <summary>
    /// Returns the suitable sample nearest to a wanted one, searching outwards in square rings, each in a fixed
    /// order: on the terrain, no steeper than <see cref="MaxSlope"/> over 2 m, and dry.
    /// </summary>
    /// <param name="field">The terrain's heights.</param>
    /// <param name="column">The wanted sample's column.</param>
    /// <param name="row">The wanted sample's row.</param>
    /// <param name="isWet">Whether the ground at a sample is under, or right next to, water.</param>
    /// <returns>The sample, or <see langword="null"/> if none is suitable.</returns>
    public static (int Column, int Row)? FindNear(TerrainHeightField field, int column, int row, Func<int, int, bool> isWet)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(isWet);
        int size = field.Size;
        int step = Math.Max(1, (int)Math.Round(StepMeters / field.Grid.SquareSize));
        int maxRing = (size / step) + 1;
        for (int ring = 0; ring <= maxRing; ring++)
        {
            int reach = ring * step;
            int firstColumn = column - reach;
            int lastColumn = column + reach;
            int firstRow = row - reach;
            int lastRow = row + reach;
            if (ring == 0)
            {
                if (IsSuitable(field, column, row, isWet))
                {
                    return (column, row);
                }

                continue;
            }

            // The ring's four sides: bottom left to right, right bottom to top, top right to left, left top to bottom.
            for (int c = firstColumn; c < lastColumn; c += step)
            {
                if (IsSuitable(field, c, firstRow, isWet))
                {
                    return (c, firstRow);
                }
            }

            for (int r = firstRow; r < lastRow; r += step)
            {
                if (IsSuitable(field, lastColumn, r, isWet))
                {
                    return (lastColumn, r);
                }
            }

            for (int c = lastColumn; c > firstColumn; c -= step)
            {
                if (IsSuitable(field, c, lastRow, isWet))
                {
                    return (c, lastRow);
                }
            }

            for (int r = lastRow; r > firstRow; r -= step)
            {
                if (IsSuitable(field, firstColumn, r, isWet))
                {
                    return (firstColumn, r);
                }
            }
        }

        return null;
    }

    private static bool IsSuitable(TerrainHeightField field, int column, int row, Func<int, int, bool> isWet) =>
        column >= 0 && row >= 0 && column < field.Size && row < field.Size
        && field.SlopeAt(column, row, 2) <= MaxSlope
        && !isWet(column, row);
}
