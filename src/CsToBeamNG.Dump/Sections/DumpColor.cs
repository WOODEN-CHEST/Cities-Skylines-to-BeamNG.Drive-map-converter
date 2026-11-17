using System;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A colour as a section stores it: the four 32-bit floats of the game's Unity <c>Color</c>, unchanged
/// (docs/dump-format.md, Vectors and curves). Record layouts write it as the fields R, G, B and A, in this order; JSON
/// files as an object with the numbers <c>r</c>, <c>g</c>, <c>b</c> and <c>a</c>.
/// </summary>
public struct DumpColor : IEquatable<DumpColor>
{
    /// <summary>Creates a colour.</summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    public DumpColor(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>The red component, usually 0 to 1.</summary>
    public float R { get; }

    /// <summary>The green component, usually 0 to 1.</summary>
    public float G { get; }

    /// <summary>The blue component, usually 0 to 1.</summary>
    public float B { get; }

    /// <summary>The alpha component, usually 0 to 1. What it means depends on where the colour comes from.</summary>
    public float A { get; }

    /// <summary>Whether all four components are finite numbers.</summary>
    public bool IsFinite => IsFiniteValue(R) && IsFiniteValue(G) && IsFiniteValue(B) && IsFiniteValue(A);

    /// <summary>Compares two colours bit for bit, except that 0 equals −0.</summary>
    public static bool operator ==(DumpColor left, DumpColor right) => left.Equals(right);

    /// <summary>The opposite of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(DumpColor left, DumpColor right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(DumpColor other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DumpColor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (((((R.GetHashCode() * 397) ^ G.GetHashCode()) * 397) ^ B.GetHashCode()) * 397) ^ A.GetHashCode();
        }
    }

    /// <summary>Returns <c>(r, g, b, a)</c> with the shortest exact digits, such as <c>(1, 0.5, 0.25, 0)</c>.</summary>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "({0}, {1}, {2}, {3})",
        JsonNumberFormat.Format(R),
        JsonNumberFormat.Format(G),
        JsonNumberFormat.Format(B),
        JsonNumberFormat.Format(A));

    private static bool IsFiniteValue(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
