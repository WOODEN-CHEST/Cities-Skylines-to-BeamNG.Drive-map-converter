using System;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A cubic Bézier curve as a section stores it: its four control points in the game's coordinates, as the game's
/// <c>ColossalFramework.Math.Bezier3</c> holds them (docs/research/cs1-networks.md, Data model). The curve starts at
/// <see cref="A"/>, ends at <see cref="D"/> and is pulled towards <see cref="B"/> and <see cref="C"/>. Record layouts
/// write it as the twelve fields aX, aY, aZ, bX, ... dZ, in this order.
/// </summary>
public struct DumpBezier3 : IEquatable<DumpBezier3>
{
    /// <summary>Creates a curve.</summary>
    /// <param name="a">The start point.</param>
    /// <param name="b">The first middle control point.</param>
    /// <param name="c">The second middle control point.</param>
    /// <param name="d">The end point.</param>
    public DumpBezier3(DumpVector3 a, DumpVector3 b, DumpVector3 c, DumpVector3 d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }

    /// <summary>The start point.</summary>
    public DumpVector3 A { get; }

    /// <summary>The first middle control point.</summary>
    public DumpVector3 B { get; }

    /// <summary>The second middle control point.</summary>
    public DumpVector3 C { get; }

    /// <summary>The end point.</summary>
    public DumpVector3 D { get; }

    /// <summary>Whether all control points are finite.</summary>
    public bool IsFinite => A.IsFinite && B.IsFinite && C.IsFinite && D.IsFinite;

    /// <summary>Compares two curves point by point.</summary>
    public static bool operator ==(DumpBezier3 left, DumpBezier3 right) => left.Equals(right);

    /// <summary>The opposite of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(DumpBezier3 left, DumpBezier3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(DumpBezier3 other) => A == other.A && B == other.B && C == other.C && D == other.D;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DumpBezier3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (((((A.GetHashCode() * 397) ^ B.GetHashCode()) * 397) ^ C.GetHashCode()) * 397) ^ D.GetHashCode();
        }
    }

    /// <summary>Returns the four points, such as <c>(0, 60, 0) (10, 60, 0) (20, 60, 0) (30, 60, 0)</c>.</summary>
    public override string ToString() => A + " " + B + " " + C + " " + D;
}
