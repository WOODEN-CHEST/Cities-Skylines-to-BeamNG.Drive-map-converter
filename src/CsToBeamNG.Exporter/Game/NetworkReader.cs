using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the game's networks (roads, tracks, paths, pipes and the rest) into the contents of the dump's <c>net-nodes</c>,
/// <c>net-segments</c>, <c>net-lanes</c> and <c>net-prefabs</c> sections, without changing anything
/// (docs/research/cs1-networks.md, Data model). Create it on the main thread; read on the simulation thread, where the
/// game changes the networks.
/// </summary>
/// <remarks>
/// Geometry the game derives, the segments' centre curves and corners, comes from the game's own methods
/// (<c>NetSegment.CalculateMiddlePoints</c> and <c>CalculateCorner</c>), called the way the game calls them. An object
/// that can't be read is skipped and counted in the <see cref="ObjectFailures"/> passed in.
/// </remarks>
internal sealed class NetworkReader
{
    // The corners as the game's renderer and NetAI.UpdateLanes compute them: with the node's height offset.
    private const bool CornersWithHeightOffset = true;

    // A segment's lane list can't be longer than this; it stops the walk along a damaged list.
    private const int MaxLanesPerSegment = 1024;

    private static readonly string[] _variantFields = ["m_elevatedInfo", "m_bridgeInfo", "m_slopeInfo", "m_tunnelInfo"];

    private readonly NetManager _net;
    private readonly Dictionary<Type, FieldInfo?[]> _variantFieldsByAi = new();

    private NetworkReader(NetManager net) => _net = net;

    /// <summary>Finds the network manager. Main thread only (<see cref="CityReader.Require{T}"/>).</summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    public static NetworkReader Create() => new(CityReader.Require<NetManager>());

    /// <summary>Reads every created node. Simulation thread only.</summary>
    /// <param name="failures">Where to count the nodes that can't be read.</param>
    public NetNodesSectionContent ReadNodes(ObjectFailures failures)
    {
        var content = new NetNodesSectionContent();
        NetNode[] nodes = _net.m_nodes.m_buffer;
        for (int i = 0; i < nodes.Length; i++)
        {
            if ((nodes[i].m_flags & NetNode.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                var record = new NetNodeRecord
                {
                    Id = (uint)i,
                    Flags = unchecked((uint)nodes[i].m_flags),
                    Flags2 = unchecked((uint)nodes[i].m_flags2),
                    Prefab = nodes[i].m_infoIndex,
                    Position = ToDump(nodes[i].m_position),
                    Building = nodes[i].m_building,
                    Elevation = nodes[i].m_elevation,
                    HeightOffset = nodes[i].m_heightOffset,
                };
                for (int slot = 0; slot < NetNodeRecord.SegmentSlots; slot++)
                {
                    record.Segments[slot] = nodes[i].GetSegment(slot);
                }

                content.Nodes.Add(record);
            }
            catch (Exception e)
            {
                failures.Add(Describe("Node", i, nodes[i].m_infoIndex) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>
    /// Reads every created segment with its centre curve and its four corners, computed by the game. Simulation thread
    /// only.
    /// </summary>
    /// <param name="failures">Where to count the segments that can't be read.</param>
    public NetSegmentsSectionContent ReadSegments(ObjectFailures failures)
    {
        var content = new NetSegmentsSectionContent();
        NetSegment[] segments = _net.m_segments.m_buffer;
        NetNode[] nodes = _net.m_nodes.m_buffer;
        for (int i = 0; i < segments.Length; i++)
        {
            if ((segments[i].m_flags & NetSegment.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                content.Segments.Add(ReadSegment((ushort)i, ref segments[i], nodes));
            }
            catch (Exception e)
            {
                failures.Add(Describe("Segment", i, segments[i].m_infoIndex) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>
    /// Reads every created lane, with its index in its segment's lane list, which follows the order of the prefab's
    /// lanes (docs/research/cs1-networks.md, Lanes). Simulation thread only.
    /// </summary>
    /// <param name="failures">Where to count the lanes that can't be read.</param>
    public NetLanesSectionContent ReadLanes(ObjectFailures failures)
    {
        NetLane[] lanes = _net.m_lanes.m_buffer;
        Dictionary<uint, ushort> indices = IndexLanes(lanes);
        var content = new NetLanesSectionContent();
        for (int i = 0; i < lanes.Length; i++)
        {
            if (((NetLane.Flags)lanes[i].m_flags & NetLane.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                if (!indices.TryGetValue((uint)i, out ushort index))
                {
                    failures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Lane {0} isn't in the lane list of its segment {1}, so its place among the prefab's lanes is unknown.",
                        i,
                        lanes[i].m_segment));
                    continue;
                }

                content.Lanes.Add(new NetLaneRecord
                {
                    Id = (uint)i,
                    Segment = lanes[i].m_segment,
                    Index = index,
                    Flags = lanes[i].m_flags,
                    Bezier = ToDump(lanes[i].m_bezier.a, lanes[i].m_bezier.b, lanes[i].m_bezier.c, lanes[i].m_bezier.d),
                    Length = lanes[i].m_length,
                });
            }
            catch (Exception e)
            {
                failures.Add("Lane " + i.ToString(CultureInfo.InvariantCulture) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>Reads the prefabs that created nodes and segments use. Simulation thread only.</summary>
    /// <param name="failures">Where to count the prefabs that can't be read, such as a missing asset's.</param>
    public NetPrefabsSectionContent ReadPrefabs(ObjectFailures failures)
    {
        var content = new NetPrefabsSectionContent();
        foreach (ushort index in UsedPrefabIndices())
        {
            try
            {
                NetInfo info = PrefabCollection<NetInfo>.GetPrefab(index);
                if (info == null)
                {
                    failures.Add(Describe("Prefab", index, index) + " isn't loaded, so its nodes and segments have no prefab.");
                    continue;
                }

                NetPrefabRecord record = ReadPrefab(index, info);
                string? problem = NetPrefabsSectionWriter.Check(record);
                if (problem is not null)
                {
                    failures.Add(Describe("Prefab", index, index) + " can't be written: " + problem + ".");
                    continue;
                }

                content.Prefabs.Add(record);
            }
            catch (Exception e)
            {
                failures.Add(Describe("Prefab", index, index) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    private static NetSegmentRecord ReadSegment(ushort id, ref NetSegment segment, NetNode[] nodes)
    {
        // The corners in the order NetAI.UpdateLanes asks for them (docs/research/cs1-networks.md, Corners).
        segment.CalculateCorner(id, CornersWithHeightOffset, true, true, out Vector3 startLeft, out Vector3 startLeftDirection, out bool startLeftSmooth);
        segment.CalculateCorner(id, CornersWithHeightOffset, false, true, out Vector3 endLeft, out Vector3 endLeftDirection, out bool endLeftSmooth);
        segment.CalculateCorner(id, CornersWithHeightOffset, true, false, out Vector3 startRight, out Vector3 startRightDirection, out bool startRightSmooth);
        segment.CalculateCorner(id, CornersWithHeightOffset, false, false, out Vector3 endRight, out Vector3 endRightDirection, out bool endRightSmooth);

        // The centre curve as the game builds it, for example in NetSegment.GetClosestPositionAndDirection: from node
        // to node, smooth at middle nodes.
        Vector3 start = nodes[segment.m_startNode].m_position;
        Vector3 end = nodes[segment.m_endNode].m_position;
        bool smoothStart = (nodes[segment.m_startNode].m_flags & NetNode.Flags.Middle) != 0;
        bool smoothEnd = (nodes[segment.m_endNode].m_flags & NetNode.Flags.Middle) != 0;
        NetSegment.CalculateMiddlePoints(start, segment.m_startDirection, end, segment.m_endDirection, smoothStart, smoothEnd, out Vector3 middle1, out Vector3 middle2);

        return new NetSegmentRecord
        {
            Id = id,
            Flags = unchecked((uint)segment.m_flags),
            Flags2 = unchecked((uint)segment.m_flags2),
            Prefab = segment.m_infoIndex,
            StartNode = segment.m_startNode,
            EndNode = segment.m_endNode,
            StartDirection = ToDump(segment.m_startDirection),
            EndDirection = ToDump(segment.m_endDirection),
            MiddlePosition = ToDump(segment.m_middlePosition),
            Bezier = ToDump(start, middle1, middle2, end),
            StartLeft = new NetCorner(ToDump(startLeft), ToDump(startLeftDirection), startLeftSmooth),
            StartRight = new NetCorner(ToDump(startRight), ToDump(startRightDirection), startRightSmooth),
            EndLeft = new NetCorner(ToDump(endLeft), ToDump(endLeftDirection), endLeftSmooth),
            EndRight = new NetCorner(ToDump(endRight), ToDump(endRightDirection), endRightSmooth),
            AverageLength = segment.m_averageLength,
        };
    }

    private static DumpVector3 ToDump(Vector3 vector) => new(vector.x, vector.y, vector.z);

    private static DumpBezier3 ToDump(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => new(ToDump(a), ToDump(b), ToDump(c), ToDump(d));

    private static string Describe(string kind, int id, ushort prefabIndex)
    {
        string name;
        try
        {
            name = PrefabCollection<NetInfo>.PrefabName(prefabIndex) ?? "an unnamed prefab";
        }
        catch (Exception e)
        {
            name = "a prefab whose name can't be read (" + e.GetType().Name + ")";
        }

        return kind + " " + id.ToString(CultureInfo.InvariantCulture) + " (\"" + name + "\")";
    }

    // Maps each lane id to its place in its segment's lane list: the segment's m_lanes, then each lane's m_nextLane.
    // NetAI.UpdateLanes fills the list in the order of the prefab's lanes.
    private Dictionary<uint, ushort> IndexLanes(NetLane[] lanes)
    {
        var indices = new Dictionary<uint, ushort>();
        NetSegment[] segments = _net.m_segments.m_buffer;
        for (int i = 0; i < segments.Length; i++)
        {
            if ((segments[i].m_flags & NetSegment.Flags.Created) == 0)
            {
                continue;
            }

            uint lane = segments[i].m_lanes;
            for (int index = 0; lane != 0 && lane < lanes.Length && index < MaxLanesPerSegment; index++)
            {
                if (!indices.ContainsKey(lane))
                {
                    indices.Add(lane, (ushort)index);
                }

                lane = lanes[lane].m_nextLane;
            }
        }

        return indices;
    }

    private List<ushort> UsedPrefabIndices()
    {
        var used = new Dictionary<ushort, bool>();
        foreach (NetNode node in _net.m_nodes.m_buffer)
        {
            if ((node.m_flags & NetNode.Flags.Created) != 0)
            {
                used[node.m_infoIndex] = true;
            }
        }

        foreach (NetSegment segment in _net.m_segments.m_buffer)
        {
            if ((segment.m_flags & NetSegment.Flags.Created) != 0)
            {
                used[segment.m_infoIndex] = true;
            }
        }

        var indices = new List<ushort>(used.Keys);
        indices.Sort();
        return indices;
    }

    private NetPrefabRecord ReadPrefab(ushort index, NetInfo info)
    {
        ItemClass itemClass = info.m_class;
        NetAI ai = info.m_netAI;
        bool hasClass = itemClass != null;
        bool hasAi = ai != null;
        var record = new NetPrefabRecord
        {
            Index = index,
            Name = info.name ?? string.Empty,
            ClassName = hasClass ? itemClass!.name ?? string.Empty : string.Empty,
            Service = hasClass ? itemClass!.m_service.ToString() : string.Empty,
            SubService = hasClass ? itemClass!.m_subService.ToString() : string.Empty,
            Level = hasClass ? itemClass!.m_level.ToString() : string.Empty,
            Layer = hasClass ? unchecked((uint)itemClass!.m_layer) : 0,
            AiType = hasAi ? ai!.GetType().FullName ?? string.Empty : string.Empty,
            Highway = hasAi && ai!.IsHighway(),
            Bridge = hasAi && ai!.IsBridge(),
            Tunnel = hasAi && ai!.IsTunnel(),
            Underground = hasAi && ai!.IsUnderground(),
            Overground = hasAi && ai!.IsOverground(),
            HalfWidth = info.m_halfWidth,
            PavementWidth = info.m_pavementWidth,
            SegmentLength = info.m_segmentLength,
            SurfaceLevel = info.m_surfaceLevel,
            CreatePavement = info.m_createPavement,
            CreateGravel = info.m_createGravel,
            CreateRuining = info.m_createRuining,
            FlattenTerrain = info.m_flattenTerrain,
            LowerTerrain = info.m_lowerTerrain,
            ClipTerrain = info.m_clipTerrain,
            FollowTerrain = info.m_followTerrain,
            FlatJunctions = info.m_flatJunctions,
        };

        if (hasAi)
        {
            string?[] variants = ReadVariants(ai!);
            record.ElevatedVariant = variants[0];
            record.BridgeVariant = variants[1];
            record.SlopeVariant = variants[2];
            record.TunnelVariant = variants[3];
        }

        NetInfo.Lane[] lanes = info.m_lanes ?? [];
        foreach (NetInfo.Lane lane in lanes)
        {
            if (lane == null)
            {
                // Recorded as a hole, so the indices of the lanes after it stay right; the writer's check rejects the prefab.
                record.Lanes.Add(null!);
                continue;
            }

            record.Lanes.Add(new NetPrefabLane
            {
                Position = lane.m_position,
                Width = lane.m_width,
                VerticalOffset = lane.m_verticalOffset,
                StopOffset = lane.m_stopOffset,
                SpeedLimit = lane.m_speedLimit,
                Direction = (byte)lane.m_direction,
                FinalDirection = (byte)lane.m_finalDirection,
                LaneType = (byte)lane.m_laneType,
                VehicleType = unchecked((uint)lane.m_vehicleType),
                StopType = unchecked((uint)lane.m_stopType),
            });
        }

        return record;
    }

    // The names of the elevated, bridge, slope and tunnel versions. Road, track, metro and path AIs keep them in fields of
    // these names (game 1.21.1-f9); mods' AIs usually derive from those. Read by reflection, so every AI type is covered.
    private string?[] ReadVariants(NetAI ai)
    {
        Type type = ai.GetType();
        if (!_variantFieldsByAi.TryGetValue(type, out FieldInfo?[]? fields))
        {
            fields = new FieldInfo?[_variantFields.Length];
            for (int i = 0; i < _variantFields.Length; i++)
            {
                FieldInfo? field = type.GetField(_variantFields[i], BindingFlags.Public | BindingFlags.Instance);
                fields[i] = field is not null && typeof(NetInfo).IsAssignableFrom(field.FieldType) ? field : null;
            }

            _variantFieldsByAi.Add(type, fields);
        }

        string?[] names = new string?[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i]?.GetValue(ai) is NetInfo variant && variant != null)
            {
                names[i] = variant.name;
            }
        }

        return names;
    }
}
