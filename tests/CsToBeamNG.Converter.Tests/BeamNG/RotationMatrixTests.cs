using System;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG.Scene;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class RotationMatrixTests
{
    [Fact]
    public void FacingNorthIsNoRotation() => Assert.Equal(RotationMatrix.Identity, RotationMatrix.YawTowards(new Vector2(0, 5)));

    [Fact]
    public void TurnsTheLocalYAxisTowardsTheDirection()
    {
        RotationMatrix east = RotationMatrix.YawTowards(new Vector2(1, 0));

        Assert.Equal(new Vector3(1, 0, 0), east.LocalYAxis);
        Assert.Equal(new Vector3(0, -1, 0), east.LocalXAxis);
        Assert.Equal(new Vector3(0, 0, 1), east.LocalZAxis);
    }

    [Fact]
    public void WritesTheColumnsOneAfterAnother()
    {
        RotationMatrix east = RotationMatrix.YawTowards(new Vector2(1, 0));

        // Local X, then local Y, then local Z, as the game reads the field (verified in BeamNG.drive 0.40.5).
        Assert.Equal("[0,-1,0,1,0,0,0,0,1]", east.ToJson().ToJsonString());
    }

    [Fact]
    public void SpawnPointsFaceTheVehicleAlongTheirLocalMinusYAxis()
    {
        RotationMatrix rotation = SpawnSphere.RotationForHeading(new Vector2(1, 0));

        Assert.Equal(new Vector3(-1, 0, 0), rotation.LocalYAxis);

        // The value that made the vehicle face east in the game.
        Assert.Equal("[0,1,0,-1,0,0,0,0,1]", rotation.ToJson().ToJsonString());
    }

    [Fact]
    public void IsARotationForAnyDirection()
    {
        RotationMatrix m = RotationMatrix.YawTowards(new Vector2(3, -4));

        Assert.Equal(new Vector3(0.6f, -0.8f, 0), m.LocalYAxis);
        Assert.Equal(0, Vector3.Dot(m.LocalXAxis, m.LocalYAxis), 6);
        Assert.Equal(1, m.LocalXAxis.Length(), 6);
        Vector3 cross = Vector3.Cross(m.LocalXAxis, m.LocalYAxis);
        Assert.Equal(m.LocalZAxis.X, cross.X, 6);
        Assert.Equal(m.LocalZAxis.Y, cross.Y, 6);
        Assert.Equal(m.LocalZAxis.Z, cross.Z, 6);
    }

    [Fact]
    public void RejectsAZeroDirection()
    {
        Assert.Throws<ArgumentException>(() => RotationMatrix.YawTowards(Vector2.Zero));
        Assert.Throws<ArgumentException>(() => RotationMatrix.YawTowards(new Vector2(float.NaN, 1)));
    }
}
