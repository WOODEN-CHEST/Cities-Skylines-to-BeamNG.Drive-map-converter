using System;
using System.Numerics;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// The vertex data of a network mesh, in the game's coordinates (Y up, metres). The optional arrays have
/// one entry per vertex. Colors are RGBA bytes, four per vertex; the bending only uses green, which says how
/// far a vertex takes its texture V coordinate from the curve.
/// </summary>
public sealed class NetMeshData
{
    /// <summary>Creates mesh data; the optional arrays must have one entry per vertex.</summary>
    /// <param name="vertices">The positions.</param>
    /// <param name="normals">The normals, if any.</param>
    /// <param name="tangents">The tangents (W is the handedness), if any.</param>
    /// <param name="uvs">The texture coordinates, if any.</param>
    /// <param name="colors">The colors, four bytes per vertex, if any.</param>
    /// <param name="triangles">The triangles' vertex indices, three per triangle.</param>
    public NetMeshData(Vector3[] vertices, Vector3[]? normals, Vector4[]? tangents, Vector2[]? uvs, byte[]? colors, int[] triangles)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(triangles);
        int count = vertices.Length;
        if ((normals is not null && normals.Length != count) || (tangents is not null && tangents.Length != count)
            || (uvs is not null && uvs.Length != count) || (colors is not null && colors.Length != count * 4))
        {
            throw new ArgumentException("Every vertex array must have one entry per vertex.");
        }

        Vertices = vertices;
        Normals = normals;
        Tangents = tangents;
        Uvs = uvs;
        Colors = colors;
        Triangles = triangles;
    }

    /// <summary>The positions.</summary>
    public Vector3[] Vertices { get; }

    /// <summary>The normals, if any.</summary>
    public Vector3[]? Normals { get; }

    /// <summary>The tangents, if any; W is the handedness.</summary>
    public Vector4[]? Tangents { get; }

    /// <summary>The texture coordinates, if any.</summary>
    public Vector2[]? Uvs { get; }

    /// <summary>The colors, four bytes (RGBA) per vertex, if any.</summary>
    public byte[]? Colors { get; }

    /// <summary>The triangles' vertex indices, three per triangle.</summary>
    public int[] Triangles { get; }

    /// <summary>The number of vertices.</summary>
    public int VertexCount => Vertices.Length;

    /// <summary>The green channel of a vertex's color as 0–1, or 1 when the mesh has no colors (as the game assumes white).</summary>
    /// <param name="vertex">The vertex index.</param>
    public float Green(int vertex) => Colors is null ? 1f : Colors[(vertex * 4) + 1] * (1f / 255f);
}
