using System;
using System.Globalization;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// Converts between heights in metres and the stored 16-bit heights of a <c>.ter</c> file. A stored height h stands
/// for h × <see cref="MaxHeight"/> / 65536 above the <c>TerrainBlock</c>'s z, so the largest storable height, 65535,
/// is one step below <see cref="MaxHeight"/> (verified in BeamNG.drive 0.40.5; docs/research/beamng-level-format.md,
/// Terrain).
/// </summary>
public sealed record TerrainHeightScale
{
    /// <summary>The divisor that maps stored heights to fractions of <see cref="MaxHeight"/>.</summary>
    public const int Divisor = 65536;

    /// <summary>Creates a scale.</summary>
    /// <param name="maxHeight">The <c>TerrainBlock</c>'s <c>maxHeight</c>, in metres.</param>
    public TerrainHeightScale(float maxHeight)
    {
        if (!(maxHeight > 0) || !float.IsFinite(maxHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(maxHeight), maxHeight, "The maximum height must be positive.");
        }

        MaxHeight = maxHeight;
    }

    /// <summary>The <c>TerrainBlock</c>'s <c>maxHeight</c>, in metres.</summary>
    public float MaxHeight { get; }

    /// <summary>The height in metres that one step of the stored height represents.</summary>
    public double Resolution => (double)MaxHeight / Divisor;

    /// <summary>The highest height the terrain can store, in metres above the <c>TerrainBlock</c>'s z.</summary>
    public double HighestHeight => ToMeters(ushort.MaxValue);

    /// <summary>The stored height closest to a height.</summary>
    /// <param name="heightMeters">The height above the <c>TerrainBlock</c>'s z, from 0 to <see cref="HighestHeight"/>.</param>
    /// <returns>The stored height.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The height is outside the range the terrain can store.</exception>
    public ushort ToStored(double heightMeters)
    {
        double stored = Math.Round(heightMeters / MaxHeight * Divisor, MidpointRounding.AwayFromZero);
        if (!(stored >= 0) || stored > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMeters),
                heightMeters,
                string.Create(CultureInfo.InvariantCulture, $"Terrain heights must lie between 0 and {HighestHeight} m."));
        }

        return (ushort)stored;
    }

    /// <summary>The height a stored height represents.</summary>
    /// <param name="stored">The stored height.</param>
    /// <returns>The height above the <c>TerrainBlock</c>'s z, in metres.</returns>
    public double ToMeters(ushort stored) => (double)stored * MaxHeight / Divisor;
}
