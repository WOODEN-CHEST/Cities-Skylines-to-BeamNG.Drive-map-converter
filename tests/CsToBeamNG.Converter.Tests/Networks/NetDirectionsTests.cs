using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Networks;

public sealed class NetDirectionsTests
{
    private static readonly NetSegmentRecord _plain = new();
    private static readonly NetSegmentRecord _inverted = new() { Flags = NetSegmentRecord.InvertFlag };

    [Theory]
    [InlineData(NetPrefabLane.ForwardDirection, false, NetPrefabLane.ForwardDirection)]
    [InlineData(NetPrefabLane.ForwardDirection, true, NetPrefabLane.BackwardDirection)]
    [InlineData(NetPrefabLane.BackwardDirection, true, NetPrefabLane.ForwardDirection)]
    [InlineData(NetPrefabLane.BothDirections, true, NetPrefabLane.BothDirections)]
    [InlineData(0x0B, false, NetPrefabLane.BothDirections)]
    [InlineData(0x07, true, NetPrefabLane.BothDirections)]
    [InlineData(0, true, 0)]
    public void ReversesLanesOnInvertedSegments(byte finalDirection, bool inverted, byte expected)
    {
        NetPrefabLane lane = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, NetPrefabLane.CarVehicles, finalDirection);

        Assert.Equal(expected, NetDirections.OnSegment(inverted ? _inverted : _plain, lane));
    }

    [Fact]
    public void FindsOneWaySegments()
    {
        NetPrefabRecord oneWay = TestNetwork.Road(1, "One-Way Road", oneWay: true);
        NetPrefabRecord twoWay = TestNetwork.Road(2, "Basic Road");
        NetPrefabRecord path = TestNetwork.Other(3, "Path", "Beautification", [TestNetwork.Lane(0, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.ForwardDirection)]);

        Assert.Equal(NetOneWay.StartToEnd, NetDirections.OneWayOf(_plain, oneWay));
        Assert.Equal(NetOneWay.EndToStart, NetDirections.OneWayOf(_inverted, oneWay));
        Assert.Equal(NetOneWay.None, NetDirections.OneWayOf(_plain, twoWay));
        Assert.Equal(NetOneWay.None, NetDirections.OneWayOf(_plain, path));
    }

    [Fact]
    public void UsesTheFinalDirectionOfLeftHandTraffic()
    {
        // In a city that drives on the left the game reverses the vehicle lanes' final direction.
        NetPrefabRecord oneWay = TestNetwork.Road(1, "One-Way Road", oneWay: true);
        foreach (NetPrefabLane lane in oneWay.Lanes)
        {
            if (lane.LaneType == NetPrefabLane.VehicleLane)
            {
                lane.FinalDirection = NetPrefabLane.BackwardDirection;
            }
        }

        Assert.Equal(NetOneWay.EndToStart, NetDirections.OneWayOf(_plain, oneWay));
    }
}
