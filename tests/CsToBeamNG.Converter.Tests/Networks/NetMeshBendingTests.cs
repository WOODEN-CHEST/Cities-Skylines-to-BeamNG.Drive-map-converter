using System;
using System.Numerics;
using CsToBeamNG.Converter.Networks;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Networks;

public sealed class NetMeshBendingTests
{
    private const float HalfWidth = 8f;
    private const float Length = 64f;
    private const float Tolerance = 1e-4f;

    // A straight road along +Z, centred on the origin, as the game's matrices describe one: control points at
    // thirds, so the curve is linear in t. V runs 0..4 along it.
    private static readonly NetControlMatrix _straightLeft = Line(new Vector3(-HalfWidth, 0f, -Length / 2), new Vector3(-HalfWidth, 0f, Length / 2));
    private static readonly NetControlMatrix _straightRight = Line(new Vector3(HalfWidth, 0f, -Length / 2), new Vector3(HalfWidth, 0f, Length / 2));
    private static readonly Vector4 _segmentScale = new(0.5f / HalfWidth, 1f / Length, 1f, 1f);

    [Fact]
    public void BernsteinWeightsSumToOneAndTheirDerivativesToZero()
    {
        foreach (float t in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.9f, 1f })
        {
            Vector4 weights = NetMeshBending.Bernstein(t);
            Vector4 derivative = NetMeshBending.BernsteinDerivative(t);
            Assert.Equal(1f, weights.X + weights.Y + weights.Z + weights.W, 5);
            Assert.Equal(0f, derivative.X + derivative.Y + derivative.Z + derivative.W, 5);
        }

        Assert.Equal(new Vector4(1f, 0f, 0f, 0f), NetMeshBending.Bernstein(0f));
        Assert.Equal(new Vector4(0f, 0f, 0f, 1f), NetMeshBending.Bernstein(1f));
    }

    [Fact]
    public void ReadsMatricesRowByRow()
    {
        float[] rows =
        [
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f,
        ];

        NetControlMatrix matrix = NetControlMatrix.FromRows(rows);

        Assert.Equal(new Vector3(1f, 5f, 9f), matrix.A);
        Assert.Equal(new Vector3(4f, 8f, 12f), matrix.D);
        Assert.Equal(new Vector4(13f, 14f, 15f, 16f), matrix.V);
        Assert.Equal(new Vector4(2f, 6f, 10f, 14f), matrix.Evaluate(new Vector4(0f, 1f, 0f, 0f)));
        Assert.Throws<ArgumentException>(() => NetControlMatrix.FromRows(new float[15]));
    }

    [Fact]
    public void AStraightSegmentKeepsTheMeshAsItIs()
    {
        NetMeshData mesh = Mesh(new Vector3(-8f, 0.2f, -32f), new Vector3(3f, -0.3f, 10f), new Vector3(8f, 0f, 32f));

        NetMeshData bent = NetMeshBending.BendSegment(mesh, _straightLeft, _straightRight, _segmentScale);

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            AssertClose(mesh.Vertices[i], bent.Vertices[i]);
            AssertClose(Vector3.UnitY, bent.Normals![i]);
            AssertClose(new Vector4(1f, 0f, 0f, 1f), bent.Tangents![i]);
        }
    }

    [Fact]
    public void TakesVFromTheCurveWhereTheVertexIsGreen()
    {
        NetMeshData mesh = new(
            [new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)],
            null,
            null,
            [new Vector2(0.3f, 0.7f), new Vector2(0.3f, 0.7f)],
            [255, 255, 255, 255, 255, 0, 255, 255],
            [0, 1, 0]);

        NetMeshData bent = NetMeshBending.BendSegment(mesh, _straightLeft, _straightRight, _segmentScale);

        // The middle of the curve has V = 2; a vertex without green keeps its own V.
        Assert.Equal(0.3f, bent.Uvs![0].X);
        Assert.Equal(2f, bent.Uvs[0].Y, 5);
        Assert.Equal(new Vector2(0.3f, 0.7f), bent.Uvs[1]);
    }

    [Fact]
    public void FollowsACurveAndTurnsTheNormalsWithIt()
    {
        // A quarter circle of radius 50 around (50, 0, 0), from the origin heading +Z to (50, 0, 50) heading +X.
        const float K = 0.5522847f;
        NetControlMatrix left = Curve(50f + HalfWidth);
        NetControlMatrix right = Curve(50f - HalfWidth);
        NetMeshData mesh = Mesh(new Vector3(0f, 1f, -Length / 2), new Vector3(0f, 1f, Length / 2), new Vector3(-HalfWidth, 0f, 0f));

        NetMeshData bent = NetMeshBending.BendSegment(mesh, left, right, _segmentScale);

        AssertClose(new Vector3(0f, 1f, 0f), bent.Vertices[0]);
        AssertClose(new Vector3(50f, 1f, 50f), bent.Vertices[1]);

        // Halfway, the left edge is on the outer circle, 45° round.
        float outer = 50f + HalfWidth;
        AssertClose(new Vector3(50f - (outer * MathF.Sqrt(0.5f)), 0f, outer * MathF.Sqrt(0.5f)), bent.Vertices[2], 0.05f);
        AssertClose(Vector3.UnitY, bent.Normals![0]);

        static NetControlMatrix Curve(float radius) => new(
            new Vector3(50f - radius, 0f, 0f),
            new Vector3(50f - radius, 0f, radius * K),
            new Vector3(50f - (radius * K), 0f, radius),
            new Vector3(50f, 0f, radius),
            Vector4.Zero);
    }

    [Fact]
    public void ANegativeScaleTurnsTheMeshAround()
    {
        NetMeshData mesh = Mesh(new Vector3(-8f, 0f, -32f));
        Vector4 turned = new(-_segmentScale.X, -_segmentScale.Y, 1f, 1f);

        NetMeshData bent = NetMeshBending.BendSegment(mesh, _straightLeft, _straightRight, turned);

        AssertClose(new Vector3(8f, 0f, 32f), bent.Vertices[0]);
    }

    [Fact]
    public void BendNodeMeshesMirrorTheirNormalsWhenTurnedAround()
    {
        var mesh = new NetMeshData([new Vector3(0f, 0f, 0f)], [Vector3.Normalize(new Vector3(1f, 1f, 1f))], null, null, null, []);
        Vector4 turned = new(-_segmentScale.X, -_segmentScale.Y, 1f, 1f);

        Vector3 segmentNormal = NetMeshBending.BendSegment(mesh, _straightLeft, _straightRight, turned).Normals![0];
        Vector3 nodeNormal = NetMeshBending.BendNodeMesh(mesh, _straightLeft, _straightRight, turned).Normals![0];

        AssertClose(mesh.Normals![0], segmentNormal);
        AssertClose(new Vector3(-mesh.Normals[0].X, mesh.Normals[0].Y, -mesh.Normals[0].Z), nodeNormal);
    }

    [Fact]
    public void AJunctionMeshStartsOnTheCurvesAndEndsAtTheCentre()
    {
        const float Pavement = 0.1f;
        var meshScale = new Vector4(0.5f / HalfWidth, 1f / Length, 0.5f - Pavement, Pavement);
        var centerPos = new Vector4(0f, 0.5f, 40f, 3f);
        var sideScale = new Vector4(Pavement, 1f, Pavement, 1f);
        NetMeshData mesh = Mesh(new Vector3(-4f, 0.2f, -Length / 2), new Vector3(0f, 0.2f, Length / 2), new Vector3(6f, 0f, -Length / 2));

        NetMeshData bent = NetMeshBending.BendJunction(mesh, _straightLeft, _straightRight, _straightLeft, _straightRight, meshScale, centerPos, sideScale);

        // t = 0: on the curves' start, across the road as in the mesh.
        AssertClose(new Vector3(-4f, 0.2f, -Length / 2), bent.Vertices[0]);
        AssertClose(new Vector3(6f, 0f, -Length / 2), bent.Vertices[2]);

        // The far end of the mesh's middle line goes to the centre position.
        AssertClose(new Vector3(0f, 0.7f, 40f), bent.Vertices[1]);
    }

    [Fact]
    public void AJunctionMeshMatchesItsNeighboursPavementNearTheNode()
    {
        // The neighbour has a wider pavement share (0.2 instead of 0.1): near the node, the road/pavement
        // border (|x| at 0.4 of the full width) moves to 0.3, while the outer edge stays.
        const float Pavement = 0.1f;
        var meshScale = new Vector4(0.5f / HalfWidth, 1f / Length, 0.5f - Pavement, Pavement);
        var sideScale = new Vector4(0.2f, 1f, 0.2f, 1f);
        float border = 0.4f / meshScale.X;
        float z = 0.5f / meshScale.Y; // t = 1 before halving: the remapping is complete.

        // Far from the centre, so the centre blend doesn't interfere.
        var centerPos = new Vector4(0f, 0f, 1000f, 0f);
        NetMeshData mesh = Mesh(new Vector3(border, 0f, z), new Vector3(HalfWidth, 0f, z));
        NetMeshData same = NetMeshBending.BendJunction(mesh, _straightLeft, _straightRight, _straightLeft, _straightRight, meshScale, centerPos, new Vector4(Pavement, 1f, Pavement, 1f));
        NetMeshData wider = NetMeshBending.BendJunction(mesh, _straightLeft, _straightRight, _straightLeft, _straightRight, meshScale, centerPos, sideScale);

        // With equal proportions nothing moves across the road; with the wider pavement the border moves in.
        Assert.Equal(border, same.Vertices[0].X, 3);
        Assert.Equal(0.3f / meshScale.X, wider.Vertices[0].X, 3);
        Assert.Equal(HalfWidth, wider.Vertices[1].X, 3);
    }

    private static NetControlMatrix Line(Vector3 start, Vector3 end) => new(
        start,
        Vector3.Lerp(start, end, 1f / 3f),
        Vector3.Lerp(start, end, 2f / 3f),
        end,
        new Vector4(0f, 4f / 3f, 8f / 3f, 4f));

    private static NetMeshData Mesh(params Vector3[] vertices)
    {
        var normals = new Vector3[vertices.Length];
        var tangents = new Vector4[vertices.Length];
        Array.Fill(normals, Vector3.UnitY);
        Array.Fill(tangents, new Vector4(1f, 0f, 0f, 1f));
        return new NetMeshData(vertices, normals, tangents, null, null, []);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance = Tolerance) =>
        Assert.True((expected - actual).Length() <= tolerance, $"Expected {expected}, got {actual}.");

    private static void AssertClose(Vector4 expected, Vector4 actual) =>
        Assert.True((expected - actual).Length() <= Tolerance, $"Expected {expected}, got {actual}.");
}
