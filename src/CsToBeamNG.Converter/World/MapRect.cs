using System;
using System.Globalization;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// An axis-aligned rectangle on the map, in CS1's horizontal world coordinates: X and Z in metres
/// (docs/dump-format.md, Data). Seen from above, +X points right and +Z up, in both games
/// (docs/architecture.md, Coordinate systems).
/// </summary>
public readonly record struct MapRect
{
    /// <summary>Creates a rectangle. It may have zero width or depth.</summary>
    /// <param name="minX">The smallest X, in metres.</param>
    /// <param name="minZ">The smallest Z, in metres.</param>
    /// <param name="maxX">The largest X, in metres.</param>
    /// <param name="maxZ">The largest Z, in metres.</param>
    /// <exception cref="ArgumentException">A value isn't finite, or a minimum exceeds its maximum.</exception>
    public MapRect(float minX, float minZ, float maxX, float maxZ)
    {
        if (!(float.IsFinite(minX) && float.IsFinite(minZ) && float.IsFinite(maxX) && float.IsFinite(maxZ)))
        {
            throw new ArgumentException("A map rectangle's coordinates must be finite.");
        }

        if (minX > maxX || minZ > maxZ)
        {
            throw new ArgumentException(string.Create(
                CultureInfo.InvariantCulture,
                $"A map rectangle's minimum can't exceed its maximum: x {minX} to {maxX}, z {minZ} to {maxZ}."));
        }

        MinX = minX;
        MinZ = minZ;
        MaxX = maxX;
        MaxZ = maxZ;
    }

    /// <summary>The smallest X, in metres.</summary>
    public float MinX { get; }

    /// <summary>The smallest Z, in metres.</summary>
    public float MinZ { get; }

    /// <summary>The largest X, in metres.</summary>
    public float MaxX { get; }

    /// <summary>The largest Z, in metres.</summary>
    public float MaxZ { get; }

    /// <summary>The extent along X, in metres.</summary>
    public float Width => MaxX - MinX;

    /// <summary>The extent along Z, in metres.</summary>
    public float Depth => MaxZ - MinZ;

    /// <summary>Whether the rectangle has no area: zero width or depth.</summary>
    public bool IsEmpty => !(Width > 0 && Depth > 0);

    /// <summary>Returns the smallest rectangle that contains this one and another.</summary>
    /// <param name="other">The other rectangle.</param>
    public MapRect Union(MapRect other) =>
        new(Math.Min(MinX, other.MinX), Math.Min(MinZ, other.MinZ), Math.Max(MaxX, other.MaxX), Math.Max(MaxZ, other.MaxZ));

    /// <summary>Returns the overlap of this rectangle and another, or <see langword="null"/> if they don't overlap.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The overlap. Rectangles that only touch overlap in a rectangle of zero width or depth.</returns>
    public MapRect? Intersect(MapRect other)
    {
        float minX = Math.Max(MinX, other.MinX);
        float minZ = Math.Max(MinZ, other.MinZ);
        float maxX = Math.Min(MaxX, other.MaxX);
        float maxZ = Math.Min(MaxZ, other.MaxZ);
        return minX <= maxX && minZ <= maxZ ? new MapRect(minX, minZ, maxX, maxZ) : null;
    }

    /// <summary>Returns the rectangle grown by a margin on every side.</summary>
    /// <param name="marginMeters">The margin in metres, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">The margin is negative or not finite.</exception>
    public MapRect Expand(float marginMeters)
    {
        if (!(marginMeters >= 0) || !float.IsFinite(marginMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(marginMeters), marginMeters, "The margin must be finite and not negative.");
        }

        return new MapRect(MinX - marginMeters, MinZ - marginMeters, MaxX + marginMeters, MaxZ + marginMeters);
    }

    /// <summary>Returns the ranges, such as <c>x -3840 to 1920, z -1920 to 1920</c>, rounded to centimetres.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"x {MinX:0.##} to {MaxX:0.##}, z {MinZ:0.##} to {MaxZ:0.##}");
}
