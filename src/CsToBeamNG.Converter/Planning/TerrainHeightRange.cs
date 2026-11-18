using System;
using System.Globalization;
using CsToBeamNG.Converter.BeamNG.Terrain;

namespace CsToBeamNG.Converter.Planning;

/// <summary>
/// The heights a terrain can store: from the <c>TerrainBlock</c>'s z, <see cref="ZOffset"/>, up to
/// <see cref="MaxHeight"/> above it, in 65,536 steps (docs/research/beamng-level-format.md, Terrain). BeamNG's z
/// equals the city's height (docs/architecture.md, Coordinate systems), so the offset only shifts what is stored.
/// </summary>
/// <param name="ZOffset">The height of a stored 0, in metres: the <c>TerrainBlock</c>'s z.</param>
/// <param name="MaxHeight">The <c>TerrainBlock</c>'s <c>maxHeight</c>, in metres.</param>
public sealed record TerrainHeightRange(float ZOffset, float MaxHeight)
{
    /// <summary>The room left below the lowest and above the highest height, in metres.</summary>
    public const double MarginMeters = 1;

    /// <summary>The stored heights' scale.</summary>
    public TerrainHeightScale Scale => new(MaxHeight);

    /// <summary>The height one stored step stands for, in metres.</summary>
    public double StepMeters => Scale.Resolution;

    /// <summary>
    /// Plans the range for heights from <paramref name="lowest"/> to <paramref name="highest"/>: whole metres, with
    /// <see cref="MarginMeters"/> to spare on both sides.
    /// </summary>
    /// <param name="lowest">The lowest height, in metres.</param>
    /// <param name="highest">The highest height, in metres, not below <paramref name="lowest"/>.</param>
    /// <exception cref="ArgumentException">The heights aren't finite or are in the wrong order.</exception>
    public static TerrainHeightRange Plan(double lowest, double highest)
    {
        if (!double.IsFinite(lowest) || !double.IsFinite(highest) || highest < lowest)
        {
            throw new ArgumentException(string.Create(
                CultureInfo.InvariantCulture, $"Invalid height range {lowest} to {highest} m."));
        }

        double zOffset = Math.Floor(lowest - MarginMeters);
        double maxHeight = Math.Ceiling(highest + MarginMeters - zOffset);
        return new TerrainHeightRange((float)zOffset, (float)maxHeight);
    }

    /// <summary>The stored value closest to a height.</summary>
    /// <param name="heightMeters">The height, in metres, within the range.</param>
    public ushort ToStored(double heightMeters) => Scale.ToStored(heightMeters - ZOffset);

    /// <summary>The height a stored value stands for, in metres.</summary>
    /// <param name="stored">The stored value.</param>
    public double ToMeters(ushort stored) => ZOffset + Scale.ToMeters(stored);
}
