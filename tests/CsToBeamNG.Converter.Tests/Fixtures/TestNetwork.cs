using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Tests.Fixtures;

/// <summary>
/// Builds small synthetic networks with the game's layout: prefabs with lanes, nodes, straight segments with their
/// corners where the game puts them on a free end (half the width to each side), and lanes. Only made-up data.
/// </summary>
internal sealed class TestNetwork
{
    public const float Height = 60;

    public NetPrefabsSectionContent Prefabs { get; } = new();

    public NetNodesSectionContent Nodes { get; } = new();

    public NetSegmentsSectionContent Segments { get; } = new();

    public NetLanesSectionContent Lanes { get; } = new();

    /// <summary>
    /// A two-way road prefab with one car lane each way (forward on the right) and pavements; level 2 is the game's
    /// two-lane roads' level.
    /// </summary>
    public static NetPrefabRecord Road(uint index, string name, string level = "Level2", float halfWidth = 8, bool highway = false, bool oneWay = false)
    {
        var prefab = new NetPrefabRecord
        {
            Index = index,
            Name = name,
            ClassName = name,
            Service = "Road",
            SubService = "None",
            Level = level,
            Layer = 1,
            AiType = "RoadAI",
            Highway = highway,
            HalfWidth = halfWidth,
            PavementWidth = 3,
            SegmentLength = 64,
            SurfaceLevel = -0.3f,
            CreatePavement = true,
            FlattenTerrain = true,
            ClipTerrain = true,
        };
        prefab.Lanes.Add(Lane(-halfWidth + 1.5f, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections));
        prefab.Lanes.Add(Lane(-2, NetPrefabLane.VehicleLane, NetPrefabLane.CarVehicles, oneWay ? NetPrefabLane.ForwardDirection : NetPrefabLane.BackwardDirection));
        prefab.Lanes.Add(Lane(2, NetPrefabLane.VehicleLane, NetPrefabLane.CarVehicles, NetPrefabLane.ForwardDirection));
        prefab.Lanes.Add(Lane(halfWidth - 1.5f, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections));
        return prefab;
    }

    /// <summary>A prefab of another kind: a train track, a pedestrian path, or a pipe without lanes.</summary>
    public static NetPrefabRecord Other(uint index, string name, string service, NetPrefabLane[] lanes, float halfWidth = 4, bool underground = false, uint layer = NetPrefabRecord.DefaultLayer)
    {
        var prefab = new NetPrefabRecord
        {
            Index = index,
            Name = name,
            ClassName = name,
            Service = service,
            SubService = "None",
            Level = "Level1",
            Layer = layer,
            AiType = "NetAI",
            Underground = underground,
            HalfWidth = halfWidth,
            SegmentLength = 64,
        };
        foreach (NetPrefabLane lane in lanes)
        {
            prefab.Lanes.Add(lane);
        }

        return prefab;
    }

    public static NetPrefabLane Lane(float position, byte laneType, uint vehicleType, byte direction) => new()
    {
        Position = position,
        Width = 3,
        SpeedLimit = 1,
        Direction = direction,
        FinalDirection = direction,
        LaneType = laneType,
        VehicleType = vehicleType,
    };

    public TestNetwork AddPrefab(NetPrefabRecord prefab)
    {
        Prefabs.Prefabs.Add(prefab);
        return this;
    }

    public NetNodeRecord AddNode(uint id, float x, float z, uint flags, uint prefab = 1, float height = Height)
    {
        var node = new NetNodeRecord { Id = id, Flags = NetNodeRecord.CreatedFlag | flags, Prefab = prefab, Position = new DumpVector3(x, height, z) };
        Nodes.Nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Adds a straight segment between two nodes added before, with its corners half the prefab's width to each side
    /// of the nodes, and one lane per prefab lane offset by the lane's position (mirrored on an inverted segment).
    /// </summary>
    public NetSegmentRecord AddStraightSegment(uint id, uint prefab, uint startNode, uint endNode, uint flags = 0, uint firstLaneId = 0)
    {
        NetPrefabRecord info = FindPrefab(prefab);
        DumpVector3 a = FindNode(startNode).Position;
        DumpVector3 d = FindNode(endNode).Position;
        double length = Math.Sqrt(Square(d.X - a.X) + Square(d.Z - a.Z));
        var along = new DumpVector3((float)((d.X - a.X) / length), 0, (float)((d.Z - a.Z) / length));
        var back = new DumpVector3(-along.X, 0, -along.Z);

        // Left of the direction (dX, dZ) is (−dZ, dX), seen from above with +X east and +Z north.
        var left = new DumpVector3(-along.Z, 0, along.X);
        var segment = new NetSegmentRecord
        {
            Id = id,
            Flags = 0x1 | flags,
            Prefab = prefab,
            StartNode = startNode,
            EndNode = endNode,
            StartDirection = along,
            EndDirection = back,
            MiddlePosition = Lerp(a, d, 0.5f),
            Bezier = new DumpBezier3(a, Lerp(a, d, 1f / 3), Lerp(a, d, 2f / 3), d),
            StartLeft = new NetCorner(Offset(a, left, info.HalfWidth), along, false),
            StartRight = new NetCorner(Offset(a, left, -info.HalfWidth), along, false),
            EndLeft = new NetCorner(Offset(d, left, -info.HalfWidth), back, false),
            EndRight = new NetCorner(Offset(d, left, info.HalfWidth), back, false),
            AverageLength = (float)length,
        };
        Segments.Segments.Add(segment);
        FindNode(startNode).Segments[Array.IndexOf(FindNode(startNode).Segments, 0u)] = id;
        FindNode(endNode).Segments[Array.IndexOf(FindNode(endNode).Segments, 0u)] = id;
        if (firstLaneId != 0)
        {
            bool inverted = (flags & NetSegmentRecord.InvertFlag) != 0;
            for (int i = 0; i < info.Lanes.Count; i++)
            {
                float position = inverted ? -info.Lanes[i].Position : info.Lanes[i].Position;
                DumpVector3 start = Offset(a, left, -position);
                DumpVector3 end = Offset(d, left, -position);
                Lanes.Lanes.Add(new NetLaneRecord
                {
                    Id = firstLaneId + (uint)i,
                    Segment = id,
                    Index = (ushort)i,
                    Flags = 0x1,
                    Bezier = new DumpBezier3(start, Lerp(start, end, 1f / 3), Lerp(start, end, 2f / 3), end),
                    Length = (float)length,
                });
            }
        }

        return segment;
    }

    public SyntheticDump AddTo(SyntheticDump dump) => dump.WithNetwork(Prefabs, Nodes, Segments, Lanes);

    private static DumpVector3 Lerp(DumpVector3 a, DumpVector3 b, float t) =>
        new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t));

    private static DumpVector3 Offset(DumpVector3 point, DumpVector3 direction, float distance) =>
        new(point.X + (direction.X * distance), point.Y, point.Z + (direction.Z * distance));

    private static double Square(double value) => value * value;

    private NetPrefabRecord FindPrefab(uint index) =>
        ((List<NetPrefabRecord>)Prefabs.Prefabs).Find(prefab => prefab.Index == index) ?? throw new ArgumentException("No prefab " + index);

    private NetNodeRecord FindNode(uint id) =>
        ((List<NetNodeRecord>)Nodes.Nodes).Find(node => node.Id == id) ?? throw new ArgumentException("No node " + id);
}
