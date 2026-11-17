using System;
using System.Numerics;
using CsToBeamNG.Converter.Buildings;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Buildings;

public sealed class BuildingLotTests
{
    private const double Tolerance = 1e-4;

    public static TheoryData<float> Angles => [0f, 0.3f, (float)(Math.PI / 2), 2f, (float)Math.PI, -1.2f, 5.5f];

    [Theory]
    [MemberData(nameof(Angles))]
    public void PutsTheCornersWhereTheGameTurnsTheLot(float angle)
    {
        var building = new BuildingRecord { Position = new DumpVector3(-1200.5f, 60, 812.25f), Angle = angle, Width = 3, Length = 4 };
        Span<(double X, double Z)> corners = stackalloc (double X, double Z)[BuildingLot.CornerCount];

        BuildingLot.Corners(building, corners);

        // The game turns buildings with Quaternion.AngleAxis(angle, Vector3.down) (BuildingAI.RefreshInstance); System.Numerics
        // computes the same quaternion independently. The lot is 24 m across and 32 m deep, its front on local +Z.
        Quaternion rotation = Quaternion.CreateFromAxisAngle(-Vector3.UnitY, angle);
        AssertNear(Turn(building, rotation, -12, 16), corners[0]);
        AssertNear(Turn(building, rotation, 12, 16), corners[1]);
        AssertNear(Turn(building, rotation, 12, -16), corners[2]);
        AssertNear(Turn(building, rotation, -12, -16), corners[3]);
    }

    [Theory]
    [MemberData(nameof(Angles))]
    public void PutsTheFrontEdgeWhereTheGameHasTheSidewalk(float angle)
    {
        var building = new BuildingRecord { Position = new DumpVector3(300, 60, -42.5f), Angle = angle, Width = 2, Length = 3 };

        (double X, double Z) front = BuildingLot.FrontMiddle(building);

        // Building.CalculateSidewalkPosition(position, angle, length, 0, 0) = position − (sin a, 0, −cos a) × length × 4.
        AssertNear((300 - (Math.Sin(angle) * 12), -42.5 + (Math.Cos(angle) * 12)), front);
    }

    [Fact]
    public void MeasuresTheLotInCellsOfEightMetres()
    {
        var building = new BuildingRecord { Width = 3, Length = 4 };

        Assert.Equal(24, BuildingLot.WidthMeters(building));
        Assert.Equal(32, BuildingLot.LengthMeters(building));
        Assert.Throws<ArgumentException>(() => BuildingLot.Corners(building, new (double, double)[3]));
    }

    [Fact]
    public void TurnsTheFrameCounterClockwiseSeenFromAbove()
    {
        var north = new ObjectFrame(new DumpVector3(10, 0, 20), 0);
        var west = new ObjectFrame(new DumpVector3(10, 0, 20), (float)(Math.PI / 2));

        Assert.Equal((1, 0), north.Right);
        Assert.Equal((0, 1), north.Forward);
        AssertNear((0, 1), west.Right);
        AssertNear((-1, 0), west.Forward);
        AssertNear((10 - 5, 20 + 2), west.ToWorld(2, 5));
    }

    private static (double X, double Z) Turn(BuildingRecord building, Quaternion rotation, float localX, float localZ)
    {
        Vector3 turned = Vector3.Transform(new Vector3(localX, 0, localZ), rotation);
        return (building.Position.X + (double)turned.X, building.Position.Z + (double)turned.Z);
    }

    private static void AssertNear((double X, double Z) expected, (double X, double Z) actual)
    {
        Assert.True(
            Math.Abs(expected.X - actual.X) < Tolerance && Math.Abs(expected.Z - actual.Z) < Tolerance,
            $"Expected {expected}, got {actual}.");
    }
}
