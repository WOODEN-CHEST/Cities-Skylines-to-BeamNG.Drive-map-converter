using System;
using System.Collections.Generic;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>Builds simple meshes.</summary>
public static class ShapeMeshes
{
    /// <summary>
    /// A box standing on the origin: it spans −size/2 to +size/2 in x and y, and 0 to size.Z in z. Each face has its
    /// own four vertices, so the normals are flat; each face's texture coordinates run from 0 to 1.
    /// </summary>
    /// <param name="size">The extent along each axis, in metres; all positive.</param>
    /// <param name="materialName">The material.</param>
    /// <returns>The mesh: 24 vertices, 12 triangles.</returns>
    public static ShapeMesh Box(Vector3 size, string materialName)
    {
        if (!(size.X > 0 && size.Y > 0 && size.Z > 0) || !float.IsFinite(size.X + size.Y + size.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A box's size must be positive.");
        }

        float x = size.X / 2;
        float y = size.Y / 2;
        float z = size.Z;

        // Each face: a corner and two edges whose cross product points outwards, so that the corners in the order
        // c, c + u, c + u + v, c + v run counter-clockwise seen from outside.
        (Vector3 Corner, Vector3 U, Vector3 V)[] faces =
        [
            (new(x, -y, 0), new(0, 2 * y, 0), new(0, 0, z)),    // +X
            (new(-x, y, 0), new(0, -2 * y, 0), new(0, 0, z)),   // -X
            (new(x, y, 0), new(-2 * x, 0, 0), new(0, 0, z)),    // +Y
            (new(-x, -y, 0), new(2 * x, 0, 0), new(0, 0, z)),   // -Y
            (new(-x, -y, z), new(2 * x, 0, 0), new(0, 2 * y, 0)), // +Z, the top
            (new(-x, y, 0), new(2 * x, 0, 0), new(0, -2 * y, 0)), // -Z, the bottom
        ];

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var textureCoordinates = new List<Vector2>();
        var indices = new List<int>();
        foreach ((Vector3 corner, Vector3 u, Vector3 v) in faces)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(u, v));
            int first = positions.Count;
            positions.AddRange([corner, corner + u, corner + u + v, corner + v]);
            normals.AddRange([normal, normal, normal, normal]);
            textureCoordinates.AddRange([new(0, 0), new(1, 0), new(1, 1), new(0, 1)]);
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        return new ShapeMesh(materialName, positions, normals, textureCoordinates, indices);
    }
}
