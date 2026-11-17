using System;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// An axis-aligned box in a prefab's own coordinates, such as the extent of its mesh: the smallest and the largest
/// corner, in metres (docs/dump-format.md, Vectors and curves). JSON files write it as an object with the vectors
/// <c>min</c> and <c>max</c>.
/// </summary>
public struct DumpBounds : IEquatable<DumpBounds>
{
    /// <summary>Creates a box.</summary>
    /// <param name="min">The corner with the smallest X, Y and Z.</param>
    /// <param name="max">The corner with the largest X, Y and Z.</param>
    public DumpBounds(DumpVector3 min, DumpVector3 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The corner with the smallest X, Y and Z.</summary>
    public DumpVector3 Min { get; }

    /// <summary>The corner with the largest X, Y and Z.</summary>
    public DumpVector3 Max { get; }

    /// <summary>Whether both corners are finite.</summary>
    public bool IsFinite => Min.IsFinite && Max.IsFinite;

    /// <summary>Compares two boxes corner by corner.</summary>
    public static bool operator ==(DumpBounds left, DumpBounds right) => left.Equals(right);

    /// <summary>The opposite of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(DumpBounds left, DumpBounds right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(DumpBounds other) => Min == other.Min && Max == other.Max;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DumpBounds other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (Min.GetHashCode() * 397) ^ Max.GetHashCode();
        }
    }

    /// <summary>Returns <c>(min) to (max)</c>, such as <c>(-8, 0, -8) to (8, 12.5, 8)</c>.</summary>
    public override string ToString() => Min + " to " + Max;
}
