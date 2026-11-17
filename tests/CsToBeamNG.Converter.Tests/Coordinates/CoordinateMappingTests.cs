using System;
using System.Globalization;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG.Scene;
using CsToBeamNG.Converter.Coordinates;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Coordinates;

public sealed class CoordinateMappingTests
{
    private const float Tolerance = 1e-5f;

    public static TheoryData<float> Angles => [0f, 0.5f, 1f, MathF.PI / 2, 2f, MathF.PI, -1.2345f, 4.5f, -7f, 100.25f];

    [Fact]
    public void SwapsTheVerticalAndTheNorthAxis() =>
        Assert.Equal(new Vector3(1.5f, -3.25f, 20f), CoordinateMapping.ToBeamNG(new Vector3(1.5f, 20f, -3.25f)));

    [Fact]
    public void KeepsTheOriginAndTheHeights()
    {
        Assert.Equal(Vector3.Zero, CoordinateMapping.ToBeamNG(Vector3.Zero));
        Assert.Equal(123.5f, CoordinateMapping.ToBeamNG(new Vector3(-8000, 123.5f, 7000)).Z);
    }

    [Fact]
    public void KeepsEastAndNorthSoTheMapIsNotMirrored()
    {
        // CS1 seen from above: +X east (right), +Z north (up). BeamNG seen from above: +X right, +Y up.
        Assert.Equal(Vector3.UnitX, CoordinateMapping.ToBeamNG(Vector3.UnitX));
        Assert.Equal(Vector3.UnitY, CoordinateMapping.ToBeamNG(Vector3.UnitZ));
        Assert.Equal(Vector3.UnitZ, CoordinateMapping.ToBeamNG(Vector3.UnitY));

        // Three points that run counter-clockwise seen from above still do.
        Vector3 a = CoordinateMapping.ToBeamNG(new Vector3(0, 5, 0));
        Vector3 b = CoordinateMapping.ToBeamNG(new Vector3(10, 5, 0));
        Vector3 c = CoordinateMapping.ToBeamNG(new Vector3(0, 5, 10));
        float turn = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        Assert.True(turn > 0);
    }

    [Fact]
    public void ConvertsPositionsBack()
    {
        Vector3[] positions = [new(1, 2, 3), new(-8640, 0, 8640), new(0.1f, -0.2f, 1e-7f)];
        foreach (Vector3 position in positions)
        {
            Assert.Equal(position, CoordinateMapping.ToCitiesSkylines(CoordinateMapping.ToBeamNG(position)));
            Assert.Equal(position, CoordinateMapping.ToBeamNG(CoordinateMapping.ToCitiesSkylines(position)));
        }

        Assert.Equal(new Vector3(4, 6, 5), CoordinateMapping.ToCitiesSkylines(new Vector3(4, 5, 6)));
    }

    [Fact]
    public void ConvertsDirectionsWithoutChangingTheirLength()
    {
        Vector3 normal = Vector3.Normalize(new Vector3(0.3f, 0.9f, -0.2f));

        Vector3 converted = CoordinateMapping.DirectionToBeamNG(normal);

        Assert.Equal(new Vector3(normal.X, normal.Z, normal.Y), converted);
        Assert.Equal(1f, converted.Length(), 6);
        Assert.Equal(Vector3.UnitZ, CoordinateMapping.DirectionToBeamNG(Vector3.UnitY));
    }

    [Fact]
    public void NegatesTheTangentHandednessSoTheBitangentKeepsItsDirection()
    {
        Vector3 normal = Vector3.UnitY;
        var tangent = new Vector4(1, 0, 0, -1);

        Vector4 converted = CoordinateMapping.TangentToBeamNG(tangent);

        Assert.Equal(new Vector4(1, 0, 0, 1), converted);

        // Unity's bitangent is cross(normal, tangent) × w; the converted one must point the same way in BeamNG.
        Vector3 bitangent = Vector3.Cross(normal, new Vector3(tangent.X, tangent.Y, tangent.Z)) * tangent.W;
        Vector3 convertedNormal = CoordinateMapping.DirectionToBeamNG(normal);
        Vector3 convertedBitangent = Vector3.Cross(convertedNormal, new Vector3(converted.X, converted.Y, converted.Z)) * converted.W;
        Assert.Equal(CoordinateMapping.DirectionToBeamNG(bitangent), convertedBitangent);
    }

    [Fact]
    public void ReversesTheWindingOfEveryTriangle()
    {
        int[] triangles = [0, 1, 2, 3, 4, 5];

        CoordinateMapping.ReverseWinding(triangles);

        Assert.Equal([0, 2, 1, 3, 5, 4], triangles);
        CoordinateMapping.ReverseWinding(triangles);
        Assert.Equal([0, 1, 2, 3, 4, 5], triangles);
        CoordinateMapping.ReverseWinding([]);
    }

    [Fact]
    public void RejectsAnIncompleteTriangle()
    {
        int[] indices = [0, 1, 2, 3];

        Assert.Throws<ArgumentException>(() => CoordinateMapping.ReverseWinding(indices));
    }

    [Fact]
    public void ConvertedTrianglesFaceTheirConvertedNormals()
    {
        // A ground triangle that Unity draws from above: its vertices run clockwise seen from above, and
        // cross(b − a, c − a) in Unity's numbers is its normal, +Y.
        Vector3[] vertices = [new(0, 0, 0), new(0, 0, 1), new(1, 0, 0)];
        Assert.Equal(Vector3.UnitY, Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]));
        int[] triangle = [0, 1, 2];

        Vector3[] converted = Array.ConvertAll(vertices, CoordinateMapping.ToBeamNG);
        CoordinateMapping.ReverseWinding(triangle);

        // BeamNG and Collada treat counter-clockwise triangles as front faces, so in their right-handed numbers
        // cross(b − a, c − a) must point along the converted normal: up.
        Vector3 a = converted[triangle[0]];
        Vector3 b = converted[triangle[1]];
        Vector3 c = converted[triangle[2]];
        Assert.Equal(CoordinateMapping.DirectionToBeamNG(Vector3.UnitY), Vector3.Cross(b - a, c - a));
    }

    [Fact]
    public void YawIsTheSameAngleAboutBeamNGsUpAxis()
    {
        Assert.Equal(0.75f, CoordinateMapping.YawToBeamNG(0.75f));
        Assert.Equal(-2f, CoordinateMapping.YawToBeamNG(-2f));
    }

    [Theory]
    [MemberData(nameof(Angles))]
    public void RotatesObjectsAsTheGameDoes(float angle)
    {
        // The game turns objects with AngleAxis(angle, Vector3.down); System.Numerics applies the same
        // quaternion formula as Unity. A point of the object's mesh must land in the same place either way.
        var gameRotation = Quaternion.CreateFromAxisAngle(-Vector3.UnitY, angle);
        RotationMatrix rotation = CoordinateMapping.RotationToBeamNG(angle);
        Vector3[] localPoints = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new(3, -1, 7)];

        foreach (Vector3 local in localPoints)
        {
            Vector3 expected = CoordinateMapping.ToBeamNG(Vector3.Transform(local, gameRotation));
            Vector3 actual = Apply(rotation, CoordinateMapping.ToBeamNG(local));
            Assert.True(Vector3.Distance(expected, actual) < Tolerance * Math.Max(1, local.Length()), string.Create(CultureInfo.InvariantCulture, $"{local} at {angle}: {expected} vs {actual}"));
        }
    }

    [Theory]
    [MemberData(nameof(Angles))]
    public void RotationsAreProperRotationsAboutTheVertical(float angle)
    {
        RotationMatrix m = CoordinateMapping.RotationToBeamNG(angle);

        Assert.Equal(Vector3.UnitZ, m.LocalZAxis);
        Assert.Equal(0f, m.LocalXAxis.Z);
        Assert.Equal(0f, m.LocalYAxis.Z);
        Assert.Equal(1f, m.LocalXAxis.Length(), 6);
        Assert.Equal(1f, m.LocalYAxis.Length(), 6);
        Assert.Equal(0f, Vector3.Dot(m.LocalXAxis, m.LocalYAxis), 6);
        Assert.Equal(1f, Vector3.Dot(Vector3.Cross(m.LocalXAxis, m.LocalYAxis), m.LocalZAxis), 6);
    }

    [Fact]
    public void NoAngleIsNoRotation() => Assert.Equal(RotationMatrix.Identity, CoordinateMapping.RotationToBeamNG(0));

    [Fact]
    public void AQuarterTurnTurnsEastToNorth()
    {
        RotationMatrix m = CoordinateMapping.RotationToBeamNG(MathF.PI / 2);

        Assert.True(Vector3.Distance(Vector3.UnitY, m.LocalXAxis) < 1e-6f);
        Assert.True(Vector3.Distance(-Vector3.UnitX, m.LocalYAxis) < 1e-6f);
    }

    [Theory]
    [InlineData(0.5f, 0x3f60a940u, 0x3ef57744u)]
    [InlineData(2f, 0xbed51133u, 0x3f68c7b7u)]
    [InlineData(-1.2345f, 0x3ea8f4dbu, 0xbf71a8e4u)]
    [InlineData(3.1415927f, 0xbf800000u, 0xb3bbbd2eu)]
    [InlineData(100.25f, 0x3f75f639u, 0xbe8df805u)]
    public void GivesTheCorrectlyRoundedSineAndCosine(float angle, uint cosBits, uint sinBits)
    {
        // The expected values are the exact cosine and sine rounded to float (Python's math library), so the
        // rotation is the same on every platform and matches a correct implementation bit for bit.
        RotationMatrix m = CoordinateMapping.RotationToBeamNG(angle);

        Assert.Equal(BitConverter.UInt32BitsToSingle(cosBits), m.M11);
        Assert.Equal(BitConverter.UInt32BitsToSingle(sinBits), m.M21);
        Assert.Equal(-BitConverter.UInt32BitsToSingle(sinBits), m.M12);
        Assert.Equal(BitConverter.UInt32BitsToSingle(cosBits), m.M22);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(2e6f)]
    public void RejectsAnglesThatArentUsable(float angle) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CoordinateMapping.RotationToBeamNG(angle));

    // world = M × local, with M's columns the local axes' world directions.
    private static Vector3 Apply(RotationMatrix m, Vector3 local) =>
        (m.LocalXAxis * local.X) + (m.LocalYAxis * local.Y) + (m.LocalZAxis * local.Z);
}
