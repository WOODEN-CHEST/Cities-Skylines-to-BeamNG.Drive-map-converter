using System;
using System.Collections.Generic;
using System.Numerics;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// A <see cref="TerrainStamp"/> shaped like a footprint made of triangles, such as a road bed or a building lot: inside,
/// the terrain takes the triangles' height (according to <see cref="Mode"/>); within <see cref="FalloffMeters"/> of the
/// footprint it blends smoothly from the height at the footprint's nearest edge back to its own.
/// </summary>
/// <remarks>
/// Vertices are city positions: X and Z on the ground, Y the height, in metres. Each sample is tested against every
/// triangle, which suits footprints of up to a few hundred triangles.
/// </remarks>
public sealed class FootprintStamp : TerrainStamp
{
    private readonly Vector3[] _vertices;

    /// <summary>Creates a stamp.</summary>
    /// <param name="triangles">The footprint's corners, three per triangle, in city coordinates; at least one triangle.</param>
    /// <param name="mode">How the footprint's height combines with the terrain's.</param>
    /// <param name="falloffMeters">The width of the blend around the footprint, in metres, 0 or more.</param>
    /// <exception cref="ArgumentException">There are no triangles, or a coordinate isn't finite.</exception>
    public FootprintStamp(IReadOnlyList<Vector3> triangles, StampMode mode, double falloffMeters)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Count == 0 || triangles.Count % 3 != 0)
        {
            throw new ArgumentException("A footprint needs whole triangles, three corners each.", nameof(triangles));
        }

        if (!(falloffMeters >= 0) || !double.IsFinite(falloffMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(falloffMeters), falloffMeters, "The falloff must be finite and not negative.");
        }

        _vertices = [.. triangles];
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;
        foreach (Vector3 vertex in _vertices)
        {
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
            {
                throw new ArgumentException("Footprint corners must be finite.", nameof(triangles));
            }

            minX = Math.Min(minX, vertex.X);
            minZ = Math.Min(minZ, vertex.Z);
            maxX = Math.Max(maxX, vertex.X);
            maxZ = Math.Max(maxZ, vertex.Z);
        }

        Mode = mode;
        FalloffMeters = falloffMeters;
        Bounds = new MapRect(minX, minZ, maxX, maxZ).Expand((float)falloffMeters);
    }

    /// <summary>How the footprint's height combines with the terrain's.</summary>
    public StampMode Mode { get; }

    /// <summary>The width of the blend around the footprint, in metres.</summary>
    public double FalloffMeters { get; }

    /// <inheritdoc/>
    public override MapRect Bounds { get; }

    /// <inheritdoc/>
    public override double Apply(double x, double z, double height)
    {
        for (int i = 0; i < _vertices.Length; i += 3)
        {
            if (TryInterpolate(_vertices[i], _vertices[i + 1], _vertices[i + 2], x, z, out double inside))
            {
                return Combine(height, inside);
            }
        }

        if (FalloffMeters <= 0)
        {
            return height;
        }

        double nearest = double.MaxValue;
        double edgeHeight = 0;
        for (int i = 0; i < _vertices.Length; i += 3)
        {
            NearestOnEdge(_vertices[i], _vertices[i + 1], x, z, ref nearest, ref edgeHeight);
            NearestOnEdge(_vertices[i + 1], _vertices[i + 2], x, z, ref nearest, ref edgeHeight);
            NearestOnEdge(_vertices[i + 2], _vertices[i], x, z, ref nearest, ref edgeHeight);
        }

        double distance = Math.Sqrt(nearest);
        if (distance >= FalloffMeters)
        {
            return height;
        }

        // A smoothstep weight: full at the edge, nothing at the falloff's end, with no kink at either end.
        double u = 1 - (distance / FalloffMeters);
        double weight = u * u * (3 - (2 * u));
        return height + ((Combine(height, edgeHeight) - height) * weight);
    }

    // The height of the triangle at (x, z) by barycentric interpolation, if (x, z) lies inside it or on its edge.
    private static bool TryInterpolate(Vector3 a, Vector3 b, Vector3 c, double x, double z, out double height)
    {
        double abX = (double)b.X - a.X;
        double abZ = (double)b.Z - a.Z;
        double acX = (double)c.X - a.X;
        double acZ = (double)c.Z - a.Z;
        double determinant = (abX * acZ) - (acX * abZ);
        height = 0;
        if (determinant == 0)
        {
            return false;
        }

        double pX = x - a.X;
        double pZ = z - a.Z;
        double u = ((pX * acZ) - (acX * pZ)) / determinant;
        double v = ((abX * pZ) - (pX * abZ)) / determinant;
        const double Tolerance = 1e-9;
        if (u < -Tolerance || v < -Tolerance || u + v > 1 + Tolerance)
        {
            return false;
        }

        height = a.Y + (u * ((double)b.Y - a.Y)) + (v * ((double)c.Y - a.Y));
        return true;
    }

    // Keeps the nearest point found so far on the segment a–b: its squared distance and its height.
    private static void NearestOnEdge(Vector3 a, Vector3 b, double x, double z, ref double nearest, ref double height)
    {
        double abX = (double)b.X - a.X;
        double abZ = (double)b.Z - a.Z;
        double lengthSquared = (abX * abX) + (abZ * abZ);
        double t = lengthSquared == 0 ? 0 : Math.Clamp((((x - a.X) * abX) + ((z - a.Z) * abZ)) / lengthSquared, 0, 1);
        double dx = x - (a.X + (t * abX));
        double dz = z - (a.Z + (t * abZ));
        double distance = (dx * dx) + (dz * dz);
        if (distance < nearest)
        {
            nearest = distance;
            height = a.Y + (t * ((double)b.Y - a.Y));
        }
    }

    private double Combine(double terrain, double footprint) => Mode switch
    {
        StampMode.Lower => Math.Min(terrain, footprint),
        StampMode.Raise => Math.Max(terrain, footprint),
        _ => footprint,
    };
}
