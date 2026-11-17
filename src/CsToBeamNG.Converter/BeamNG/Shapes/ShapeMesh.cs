using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>
/// A triangle mesh with one material, in BeamNG's axes (right-handed, Z up, metres). Triangles are
/// counter-clockwise seen from their front, as Collada expects.
/// </summary>
public sealed class ShapeMesh
{
    private readonly Vector3[] _positions;
    private readonly Vector3[] _normals;
    private readonly Vector2[] _textureCoordinates;
    private readonly int[] _indices;

    /// <summary>Creates a mesh.</summary>
    /// <param name="materialName">The material, which a <see cref="ShapeMaterial"/> of the same name defines.</param>
    /// <param name="positions">The vertex positions.</param>
    /// <param name="normals">The vertex normals, one per vertex.</param>
    /// <param name="textureCoordinates">The texture coordinates, one per vertex.</param>
    /// <param name="indices">Three vertex indices per triangle.</param>
    public ShapeMesh(
        string materialName,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> textureCoordinates,
        IReadOnlyList<int> indices)
    {
        BeamNGNames.ThrowIfNotSceneName(materialName, nameof(materialName));
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(textureCoordinates);
        ArgumentNullException.ThrowIfNull(indices);
        if (normals.Count != positions.Count || textureCoordinates.Count != positions.Count)
        {
            throw new ArgumentException("Every vertex needs a position, a normal and texture coordinates.", nameof(normals));
        }

        if (indices.Count == 0 || indices.Count % 3 != 0)
        {
            throw new ArgumentException("A mesh needs at least one triangle, three indices each.", nameof(indices));
        }

        foreach (int index in indices)
        {
            if (index < 0 || index >= positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(indices), index, $"The mesh has {positions.Count} vertices.");
            }
        }

        foreach (Vector3 position in positions)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Vertex positions must be finite: {position}."), nameof(positions));
            }
        }

        MaterialName = materialName;
        _positions = [.. positions];
        _normals = [.. normals];
        _textureCoordinates = [.. textureCoordinates];
        _indices = [.. indices];
    }

    /// <summary>The material's name.</summary>
    public string MaterialName { get; }

    /// <summary>The vertex positions.</summary>
    public IReadOnlyList<Vector3> Positions => _positions;

    /// <summary>The vertex normals.</summary>
    public IReadOnlyList<Vector3> Normals => _normals;

    /// <summary>The texture coordinates.</summary>
    public IReadOnlyList<Vector2> TextureCoordinates => _textureCoordinates;

    /// <summary>Three vertex indices per triangle.</summary>
    public IReadOnlyList<int> Indices => _indices;

    /// <summary>The number of triangles.</summary>
    public int TriangleCount => _indices.Length / 3;
}
