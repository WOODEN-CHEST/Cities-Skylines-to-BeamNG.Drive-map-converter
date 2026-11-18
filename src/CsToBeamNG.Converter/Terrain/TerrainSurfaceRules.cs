using System;
using System.Threading.Tasks;
using CsToBeamNG.Converter.Water;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// The rules that decide which <see cref="SurfaceKind"/> covers each sample of the terrain, all in one place so that
/// later steps (CS1's own surface data, Phase 4) can refine them (docs/architecture.md, D38):
/// <list type="number">
/// <item><description>slopes of at least <see cref="RockSlope"/> are rock;</description></item>
/// <item><description>ground under nearby water, or less than <see cref="ShoreHeightMeters"/> above it, is sand;</description></item>
/// <item><description>slopes of at least <see cref="DirtSlope"/> are dirt;</description></item>
/// <item><description>everything else is grass.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Slopes are rise over run: 0.7 is about 35°, 0.47 about 25°. They are measured over
/// <see cref="SlopeBaselineMeters"/> to each side, the game's detail spacing, so that the 1 m samples between its
/// points don't make the boundaries noisy.
/// </remarks>
public sealed record TerrainSurfaceRules
{
    /// <summary>The rules with their default thresholds.</summary>
    public static TerrainSurfaceRules Default { get; } = new();

    /// <summary>The slope from which the ground is rock (rise over run; 0.7 is about 35°).</summary>
    public double RockSlope { get; init; } = 0.7;

    /// <summary>The slope from which the ground is dirt (rise over run; 0.47 is about 25°).</summary>
    public double DirtSlope { get; init; } = 0.47;

    /// <summary>How far to each side of a sample its slope is measured, in metres.</summary>
    public double SlopeBaselineMeters { get; init; } = 4;

    /// <summary>How high above nearby water the ground still counts as shore, in metres.</summary>
    public double ShoreHeightMeters { get; init; } = 1.5;

    /// <summary>Decides the kind of one sample.</summary>
    /// <param name="slope">Its slope, rise over run.</param>
    /// <param name="height">Its height, in metres.</param>
    /// <param name="waterSurface">The water surface near it, in metres, or NaN if there is none.</param>
    public SurfaceKind Classify(double slope, double height, double waterSurface)
    {
        if (slope >= RockSlope)
        {
            return SurfaceKind.Rock;
        }

        if (!double.IsNaN(waterSurface) && height - waterSurface < ShoreHeightMeters)
        {
            return SurfaceKind.Sand;
        }

        return slope >= DirtSlope ? SurfaceKind.Dirt : SurfaceKind.Grass;
    }

    /// <summary>Decides the kind of every sample of a terrain. Rows are classified in parallel; the result doesn't depend on the order.</summary>
    /// <param name="field">The terrain's heights.</param>
    /// <param name="water">The water near each position, or <see langword="null"/> if the city has none.</param>
    /// <returns>One kind per sample, in the field's order.</returns>
    public SurfaceKind[] Classify(TerrainHeightField field, NearbyWater? water)
    {
        ArgumentNullException.ThrowIfNull(field);
        int size = field.Size;
        var kinds = new SurfaceKind[size * size];
        Parallel.For(0, size, row =>
        {
            for (int column = 0; column < size; column++)
            {
                double waterSurface = double.NaN;
                if (water is not null)
                {
                    (double x, double z) = field.Grid.ToCitiesSkylines(column, row);
                    waterSurface = water.SurfaceNear(x, z);
                }

                kinds[(row * size) + column] = Classify(field.SlopeAt(column, row, SlopeBaselineMeters), field[column, row], waterSurface);
            }
        });
        return kinds;
    }
}
