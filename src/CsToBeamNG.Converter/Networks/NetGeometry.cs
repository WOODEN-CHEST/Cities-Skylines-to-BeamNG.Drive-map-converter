using System;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// Measures the curves of the network sections in double precision: positions and tangents of cubic Béziers, their
/// lengths, and the length the game gives a lane (docs/research/cs1-networks.md, Lanes).
/// </summary>
public static class NetGeometry
{
    /// <summary>The number of straight pieces <see cref="ArcLength"/> measures a curve with.</summary>
    public const int ArcLengthSteps = 64;

    /// <summary>Returns the point at parameter <paramref name="t"/>, from 0 at the start to 1 at the end.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="t">The parameter.</param>
    public static (double X, double Y, double Z) Position(DumpBezier3 curve, double t)
    {
        double u = 1 - t;
        double a = u * u * u;
        double b = 3 * t * u * u;
        double c = 3 * t * t * u;
        double d = t * t * t;
        return (
            (a * curve.A.X) + (b * curve.B.X) + (c * curve.C.X) + (d * curve.D.X),
            (a * curve.A.Y) + (b * curve.B.Y) + (c * curve.C.Y) + (d * curve.D.Y),
            (a * curve.A.Z) + (b * curve.B.Z) + (c * curve.C.Z) + (d * curve.D.Z));
    }

    /// <summary>Returns the curve's derivative at parameter <paramref name="t"/>: its direction, scaled by its speed.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="t">The parameter.</param>
    public static (double X, double Y, double Z) Derivative(DumpBezier3 curve, double t)
    {
        double u = 1 - t;
        double a = 3 * u * u;
        double b = 6 * t * u;
        double c = 3 * t * t;
        return (
            (a * (curve.B.X - curve.A.X)) + (b * (curve.C.X - curve.B.X)) + (c * (curve.D.X - curve.C.X)),
            (a * (curve.B.Y - curve.A.Y)) + (b * (curve.C.Y - curve.B.Y)) + (c * (curve.D.Y - curve.C.Y)),
            (a * (curve.B.Z - curve.A.Z)) + (b * (curve.C.Z - curve.B.Z)) + (c * (curve.D.Z - curve.C.Z)));
    }

    /// <summary>
    /// Returns the curve's length, in metres: the length of the polyline through <see cref="ArcLengthSteps"/> + 1 points
    /// at equal parameter steps, which is within a millimetre for the game's curves.
    /// </summary>
    /// <param name="curve">The curve.</param>
    public static double ArcLength(DumpBezier3 curve)
    {
        double length = 0;
        (double X, double Y, double Z) previous = Position(curve, 0);
        for (int i = 1; i <= ArcLengthSteps; i++)
        {
            (double X, double Y, double Z) next = Position(curve, (double)i / ArcLengthSteps);
            length += Distance(previous, next);
            previous = next;
        }

        return length;
    }

    /// <summary>
    /// Returns the length of the curve's control polygon, |B − A| + |C − B| + |D − C|, in metres: the length the game
    /// gives a lane (<c>NetLane.UpdateLength</c>), which is longer than the curve itself where it bends.
    /// </summary>
    /// <param name="curve">The curve.</param>
    public static double ControlPolygonLength(DumpBezier3 curve) =>
        Distance(curve.A, curve.B) + Distance(curve.B, curve.C) + Distance(curve.C, curve.D);

    /// <summary>Returns the distance between two points, in metres.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other point.</param>
    public static double Distance(DumpVector3 a, DumpVector3 b) =>
        Distance((a.X, a.Y, a.Z), (b.X, b.Y, b.Z));

    /// <summary>Returns the horizontal distance between two points, ignoring their heights, in metres.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other point.</param>
    public static double HorizontalDistance(DumpVector3 a, DumpVector3 b)
    {
        double dx = (double)b.X - a.X;
        double dz = (double)b.Z - a.Z;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private static double Distance((double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double dz = b.Z - a.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
