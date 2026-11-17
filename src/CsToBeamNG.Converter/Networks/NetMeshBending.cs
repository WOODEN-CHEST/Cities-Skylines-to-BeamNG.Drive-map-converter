using System;
using System.Numerics;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// Bends Cities: Skylines' straight network meshes along curves, as the game does for roads, rails and
/// junctions. A reimplementation of the game's CPU bending functions, the static
/// <c>NetSegment.PopulateGroupData</c> and <c>NetNode.PopulateGroupData</c>, which the game uses for its far
/// level-of-detail meshes; its shaders presumably do the same for the meshes up close. Everything is in game
/// coordinates (Y up), relative to the point the control matrices were made relative to
/// (docs/research/cs1-networks.md, Bending network meshes).
/// </summary>
/// <remarks>
/// Checked against the game's own output with the geometry probe (<c>cstobeamng dev geometry-probe</c>).
/// Segment meshes whose prefab samples the terrain height (<c>m_requireHeightMap</c>) take their height from
/// the terrain in the game; this class keeps the bent height.
/// </remarks>
public static class NetMeshBending
{
    /// <summary>
    /// Bends a segment mesh between a left and a right curve. Mesh X runs across the road (scaled by
    /// <c>meshScale.X</c>, so ±half width maps to 0–1) and mesh Z along it (scaled by <c>meshScale.Y</c>);
    /// mesh Y is added to the bent height unchanged. The game's segment function; it also draws a
    /// bend node's segment meshes.
    /// </summary>
    /// <param name="mesh">The straight mesh.</param>
    /// <param name="left">The curve at the mesh's left edge (mesh X = −half width).</param>
    /// <param name="right">The curve at the mesh's right edge.</param>
    /// <param name="meshScale">X: 0.5 / half width; Y: 1 / segment length (negative X and Y mirror the mesh).</param>
    public static NetMeshData BendSegment(NetMeshData mesh, NetControlMatrix left, NetControlMatrix right, Vector4 meshScale) =>
        BendBetween(mesh, left, right, meshScale, mirrorNormals: false);

    /// <summary>
    /// Bends a direct-connect node mesh across a bend node, like <see cref="BendSegment"/>, except that a
    /// negative mesh scale also mirrors the normals and tangents: the game's two-matrix node function.
    /// </summary>
    /// <param name="mesh">The straight mesh.</param>
    /// <param name="left">The curve at the mesh's left edge.</param>
    /// <param name="right">The curve at the mesh's right edge.</param>
    /// <param name="meshScale">As for <see cref="BendSegment"/>.</param>
    public static NetMeshData BendNodeMesh(NetMeshData mesh, NetControlMatrix left, NetControlMatrix right, Vector4 meshScale) =>
        BendBetween(mesh, left, right, meshScale, mirrorNormals: true);

    /// <summary>
    /// Bends a junction or dead-end node mesh: the game's four-matrix node function. The mesh's half
    /// along −Z (t from 0 to 0.5 after scaling) is used: it runs from the segment's end into the node.
    /// Each side of the mesh (X below or above 0) blends between two curves, towards the neighbour on that
    /// side (<paramref name="left"/>/<paramref name="right"/>) and the other neighbour
    /// (<paramref name="leftB"/>/<paramref name="rightB"/>); vertices near the node's middle move to
    /// <paramref name="centerPos"/>.
    /// </summary>
    /// <param name="mesh">The straight node mesh.</param>
    /// <param name="left">The curve of the mesh's left edge towards the first neighbour.</param>
    /// <param name="right">The curve of the mesh's right edge towards the first neighbour.</param>
    /// <param name="leftB">The curve of the mesh's left edge towards the second neighbour.</param>
    /// <param name="rightB">The curve of the mesh's right edge towards the second neighbour.</param>
    /// <param name="meshScale">X: 0.5 / half width; Y: 1 / segment length; Z: the road's share of the half width (0.5 − W); W: the pavement's share.</param>
    /// <param name="centerPos">The node's centre (XYZ) and its texture V coordinate (W).</param>
    /// <param name="sideScale">Per side (XY for X &lt; 0, ZW for X ≥ 0): the pavement share and the width factor to match at the far end.</param>
    public static NetMeshData BendJunction(
        NetMeshData mesh,
        NetControlMatrix left,
        NetControlMatrix right,
        NetControlMatrix leftB,
        NetControlMatrix rightB,
        Vector4 meshScale,
        Vector4 centerPos,
        Vector4 sideScale)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int count = mesh.VertexCount;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector4[count];
        Vector2[]? uvs = mesh.Uvs is null ? null : new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 source = mesh.Vertices[i];
            float across = MathF.Abs(source.X * meshScale.X);
            float t = (source.Z * meshScale.Y) + 0.5f;

            // Near the node, the pavement and road parts are rescaled to meet the neighbours' proportions.
            Vector2 sideFactors = source.X < 0f ? new Vector2(sideScale.X, sideScale.Y) : new Vector2(sideScale.Z, sideScale.W);
            var weights = new Vector3(0.5f - sideFactors.X, sideFactors.X, sideFactors.Y);
            var parts = new Vector3(
                Clamp01(across / meshScale.Z),
                Clamp01((across - meshScale.Z) / meshScale.W),
                Clamp01(across - 0.5f));
            across = Lerp(across, Vector3.Dot(parts, weights), t * t);
            float s = (across * Sign(source.X)) + 0.5f;
            t *= 0.5f;

            float side = SmoothStep(-1f, 1f, source.X);
            float toCenter = SmoothStep(0.3f, 0.5f, t - MathF.Abs(side - 0.5f));
            Vector4 basis = Bernstein(t);
            Vector4 derivative = BernsteinDerivative(t);

            Vector4 leftPoint = Blend(left.Evaluate(basis), leftB.Evaluate(basis), side);
            Vector4 rightPoint = Blend(right.Evaluate(basis), rightB.Evaluate(basis), side);
            Vector4 point = Blend(leftPoint, rightPoint, s);
            point = Blend(point, centerPos, toCenter);

            Vector4 leftTangent = Blend(left.Evaluate(derivative), leftB.Evaluate(derivative), side);
            Vector4 rightTangent = Blend(right.Evaluate(derivative), rightB.Evaluate(derivative), side);
            Vector4 along4 = Blend(leftTangent, rightTangent, s);
            var along = new Vector3(along4.X, along4.Y, along4.Z);
            along = along.LengthSquared() < 0.01f ? Vector3.UnitZ : Normalize(along);

            vertices[i] = new Vector3(point.X, point.Y + source.Y, point.Z);
            Frame(along, out Vector3 xAxis, out Vector3 yAxis);
            normals[i] = mesh.Normals is null ? Vector3.UnitY : Transform(mesh.Normals[i], xAxis, yAxis, along);
            tangents[i] = mesh.Tangents is null ? new Vector4(1f, 0f, 0f, 1f) : TransformTangent(mesh.Tangents[i], xAxis, yAxis, along);
            if (uvs is not null)
            {
                Vector2 uv = mesh.Uvs![i];
                uvs[i] = new Vector2(uv.X, Lerp(uv.Y, MathF.Min(0.9f, point.W), mesh.Green(i)));
            }
        }

        return new NetMeshData(vertices, normals, tangents, uvs, mesh.Colors, mesh.Triangles);
    }

    private static NetMeshData BendBetween(NetMeshData mesh, NetControlMatrix left, NetControlMatrix right, Vector4 meshScale, bool mirrorNormals)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int count = mesh.VertexCount;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector4[count];
        Vector2[]? uvs = mesh.Uvs is null ? null : new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 source = mesh.Vertices[i];
            float s = (source.X * meshScale.X) + 0.5f;
            float t = (source.Z * meshScale.Y) + 0.5f;
            Vector4 basis = Bernstein(t);
            Vector4 derivative = BernsteinDerivative(t);
            Vector4 point = Blend(left.Evaluate(basis), right.Evaluate(basis), s);
            Vector4 along4 = Blend(left.Evaluate(derivative), right.Evaluate(derivative), s);
            Vector3 along = Normalize(new Vector3(along4.X, along4.Y, along4.Z));

            vertices[i] = new Vector3(point.X, point.Y + source.Y, point.Z);
            Frame(along, out Vector3 xAxis, out Vector3 yAxis);
            if (mesh.Normals is null)
            {
                normals[i] = Vector3.UnitY;
            }
            else
            {
                Vector3 normal = mesh.Normals[i];
                if (mirrorNormals)
                {
                    normal = new Vector3(meshScale.X < 0f ? -normal.X : normal.X, normal.Y, meshScale.Y < 0f ? -normal.Z : normal.Z);
                }

                normals[i] = Transform(normal, xAxis, yAxis, along);
            }

            if (mesh.Tangents is null)
            {
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);
            }
            else
            {
                Vector4 tangent = mesh.Tangents[i];
                if (mirrorNormals)
                {
                    tangent = new Vector4(meshScale.X < 0f ? -tangent.X : tangent.X, tangent.Y, meshScale.Y < 0f ? -tangent.Z : tangent.Z, tangent.W);
                }

                tangents[i] = TransformTangent(tangent, xAxis, yAxis, along);
            }

            if (uvs is not null)
            {
                Vector2 uv = mesh.Uvs![i];
                uvs[i] = new Vector2(uv.X, Lerp(uv.Y, point.W, mesh.Green(i)));
            }
        }

        return new NetMeshData(vertices, normals, tangents, uvs, mesh.Colors, mesh.Triangles);
    }

    /// <summary>The cubic Bernstein weights of the four control points at <paramref name="t"/>.</summary>
    /// <param name="t">The curve parameter, 0–1.</param>
    public static Vector4 Bernstein(float t)
    {
        float u = 1f - t;
        return new Vector4(u * u * u, 3f * t * u * u, 3f * t * t * u, t * t * t);
    }

    /// <summary>The derivatives of the cubic Bernstein weights at <paramref name="t"/>: the weights of the curve's tangent.</summary>
    /// <param name="t">The curve parameter, 0–1.</param>
    public static Vector4 BernsteinDerivative(float t)
    {
        float u = 1f - t;
        return new Vector4(-3f * u * u, 3f * u * (1f - (3f * t)), 3f * t * (2f - (3f * t)), 3f * t * t);
    }

    // The game's local frame at a point of the curve: X across (horizontal), Y up, Z along the curve.
    private static void Frame(Vector3 along, out Vector3 xAxis, out Vector3 yAxis)
    {
        xAxis = Normalize(new Vector3(along.Z, 0f, -along.X));
        yAxis = Vector3.Cross(along, xAxis);
    }

    private static Vector3 Transform(Vector3 v, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis) =>
        (xAxis * v.X) + (yAxis * v.Y) + (zAxis * v.Z);

    private static Vector4 TransformTangent(Vector4 tangent, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis)
    {
        Vector3 direction = Transform(new Vector3(tangent.X, tangent.Y, tangent.Z), xAxis, yAxis, zAxis);
        return new Vector4(direction, tangent.W);
    }

    private static Vector4 Blend(Vector4 from, Vector4 to, float amount) => from + ((to - from) * amount);

    // As Unity's Vector3.Normalize: vectors shorter than 1e-5 become zero.
    private static Vector3 Normalize(Vector3 v)
    {
        float length = v.Length();
        return length > 1e-5f ? v / length : Vector3.Zero;
    }

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

    // As Unity's Mathf.Lerp, which clamps the amount.
    private static float Lerp(float from, float to, float amount) => from + ((to - from) * Clamp01(amount));

    // As Unity's Mathf.Sign: 1 for zero.
    private static float Sign(float value) => value >= 0f ? 1f : -1f;

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float c = Clamp01((x - edge0) / (edge1 - edge0));
        return c * c * (3f - (2f * c));
    }
}
