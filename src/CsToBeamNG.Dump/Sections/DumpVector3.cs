using System;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A position or direction as a section stores it: three 32-bit floats in the game's coordinates (Unity world space,
/// metres, Y up; docs/dump-format.md, Data). Record layouts write it as the fields X, Y and Z, in this order.
/// </summary>
public struct DumpVector3 : IEquatable<DumpVector3>
{
    /// <summary>Creates a vector.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component, up.</param>
    /// <param name="z">The Z component.</param>
    public DumpVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The X component; for positions, metres east of the map's centre.</summary>
    public float X { get; }

    /// <summary>The Y component; for positions, the height in metres.</summary>
    public float Y { get; }

    /// <summary>The Z component; for positions, metres north of the map's centre.</summary>
    public float Z { get; }

    /// <summary>Whether all three components are finite numbers.</summary>
    public bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y) && IsFiniteValue(Z);

    /// <summary>Compares two vectors bit for bit, except that 0 equals −0.</summary>
    public static bool operator ==(DumpVector3 left, DumpVector3 right) => left.Equals(right);

    /// <summary>The opposite of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(DumpVector3 left, DumpVector3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(DumpVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DumpVector3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397) ^ Z.GetHashCode();
        }
    }

    /// <summary>Returns <c>(x, y, z)</c> with the shortest exact digits, such as <c>(1.5, -0.25, 8640)</c>.</summary>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "({0}, {1}, {2})",
        JsonNumberFormat.Format(X),
        JsonNumberFormat.Format(Y),
        JsonNumberFormat.Format(Z));

    private static bool IsFiniteValue(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
