using System;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// Which way traffic goes on segments and lanes, as the game decides it (docs/research/cs1-networks.md, Lanes): a lane's
/// final direction, reversed on inverted segments; forward is from the segment's start node towards its end node.
/// </summary>
public static class NetDirections
{
    // The lane types whose lanes carry vehicles, as opposed to pedestrians and parked cars.
    private const byte TrafficLaneTypes = NetPrefabLane.VehicleLane | NetPrefabLane.CargoVehicleLane | NetPrefabLane.TransportVehicleLane;

    /// <summary>
    /// Returns the direction a prefab's lane goes on a segment, relative to the segment: <see cref="NetPrefabLane.FinalDirection"/>,
    /// with forward and backward swapped if the segment is inverted (<see cref="NetSegmentRecord.InvertFlag"/>), as the
    /// game's <c>NetSegment.CountLanes</c> does. Only the forward and backward bits are kept.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="lane">One of its prefab's lanes.</param>
    public static byte OnSegment(NetSegmentRecord segment, NetPrefabLane lane)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(lane);
        int direction = lane.FinalDirection & NetPrefabLane.BothDirections;
        if (segment.HasFlags(NetSegmentRecord.InvertFlag))
        {
            direction = ((direction & NetPrefabLane.ForwardDirection) << 1) | ((direction & NetPrefabLane.BackwardDirection) >> 1);
        }

        return (byte)direction;
    }

    /// <summary>
    /// Returns whether a segment's vehicle traffic goes one way only, and which way: all its prefab's vehicle lanes go
    /// the same way. Pedestrian lanes and parking don't count.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="prefab">The segment's prefab.</param>
    public static NetOneWay OneWayOf(NetSegmentRecord segment, NetPrefabRecord prefab)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(prefab);
        bool forward = false;
        bool backward = false;
        foreach (NetPrefabLane lane in prefab.Lanes)
        {
            if ((lane.LaneType & TrafficLaneTypes) == 0 || lane.VehicleType == 0)
            {
                continue;
            }

            byte direction = OnSegment(segment, lane);
            forward |= (direction & NetPrefabLane.ForwardDirection) != 0;
            backward |= (direction & NetPrefabLane.BackwardDirection) != 0;
        }

        return forward == backward ? NetOneWay.None : forward ? NetOneWay.StartToEnd : NetOneWay.EndToStart;
    }
}
