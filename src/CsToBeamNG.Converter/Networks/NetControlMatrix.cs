using System;
using System.Numerics;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// One of the control matrices Cities: Skylines bends its network meshes with: four cubic Bézier control
/// points (the matrix's columns, rows 0–2) relative to a reference point, and a texture V coordinate for
/// each of them (row 3). Game coordinates, Y up (docs/research/cs1-networks.md, Bending network meshes).
/// </summary>
public readonly struct NetControlMatrix : IEquatable<NetControlMatrix>
{
    /// <summary>Creates a matrix from its control points and their V coordinates.</summary>
    /// <param name="a">The first control point: the curve's start.</param>
    /// <param name="b">The second control point.</param>
    /// <param name="c">The third control point.</param>
    /// <param name="d">The fourth control point: the curve's end.</param>
    /// <param name="v">The texture V coordinate at each control point.</param>
    public NetControlMatrix(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 v)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        V = v;
    }

    /// <summary>The curve's start.</summary>
    public Vector3 A { get; }

    /// <summary>The second control point.</summary>
    public Vector3 B { get; }

    /// <summary>The third control point.</summary>
    public Vector3 C { get; }

    /// <summary>The curve's end.</summary>
    public Vector3 D { get; }

    /// <summary>The texture V coordinate at each control point, in the order A, B, C, D.</summary>
    public Vector4 V { get; }

    /// <summary>Reads a matrix written row by row (m00, m01, m02, m03, m10, ... m33), as Unity indexes it.</summary>
    /// <param name="rows">Sixteen numbers.</param>
    public static NetControlMatrix FromRows(ReadOnlySpan<float> rows)
    {
        if (rows.Length != 16)
        {
            throw new ArgumentException("A control matrix has 16 numbers.", nameof(rows));
        }

        return new NetControlMatrix(
            new Vector3(rows[0], rows[4], rows[8]),
            new Vector3(rows[1], rows[5], rows[9]),
            new Vector3(rows[2], rows[6], rows[10]),
            new Vector3(rows[3], rows[7], rows[11]),
            new Vector4(rows[12], rows[13], rows[14], rows[15]));
    }

    /// <summary>
    /// Multiplies the matrix with a vector of weights, one per control point, as the game does with
    /// <c>Matrix4x4 * Vector4</c>: the weighted sum of the control points, with the weighted V as W.
    /// </summary>
    /// <param name="weights">The weights of A, B, C and D.</param>
    public Vector4 Evaluate(Vector4 weights) => new(
        (A.X * weights.X) + (B.X * weights.Y) + (C.X * weights.Z) + (D.X * weights.W),
        (A.Y * weights.X) + (B.Y * weights.Y) + (C.Y * weights.Z) + (D.Y * weights.W),
        (A.Z * weights.X) + (B.Z * weights.Y) + (C.Z * weights.Z) + (D.Z * weights.W),
        (V.X * weights.X) + (V.Y * weights.Y) + (V.Z * weights.Z) + (V.W * weights.W));

    /// <inheritdoc/>
    public bool Equals(NetControlMatrix other) => A == other.A && B == other.B && C == other.C && D == other.D && V == other.V;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NetControlMatrix other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(A, B, C, D, V);

    /// <summary>Whether two matrices are equal.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    public static bool operator ==(NetControlMatrix left, NetControlMatrix right) => left.Equals(right);

    /// <summary>Whether two matrices differ.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    public static bool operator !=(NetControlMatrix left, NetControlMatrix right) => !left.Equals(right);
}
