using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

namespace CsToBeamNG.Exporter.Probes;

/// <summary>
/// Computes the pieces the game bends into a render group for a segment or node, with the inputs of its
/// static bending functions, the same way the game's instance <c>NetSegment.PopulateGroupData</c> and
/// <c>NetNode.PopulateGroupData</c> do (game 1.21.1-f9), and runs those functions for each piece. The game
/// uses these methods to build the far level-of-detail meshes, on the simulation thread.
/// </summary>
/// <remarks>
/// <para>
/// Research facts, in docs/research/cs1-networks.md (Bending network meshes). The corners,
/// middle points and control matrices come from the game's own methods; this class only repeats the glue
/// between them, which the probe validates: the pieces' outputs together must equal the game's output for
/// the whole object.
/// </para>
/// <para>
/// Call it on the simulation thread. The game's methods read the network and write only into the buffers
/// passed to them; the instance methods also use the game's static scratch buffers for the simulation
/// thread, as the game itself does there. Junctions' direct-connect meshes (tram and train tracks) are left
/// out.
/// </para>
/// </remarks>
internal static class NetGroupPieces
{
    // The holder ids the game passes to RenderManager.GetColorLocation for segments and nodes.
    private const uint SegmentColorHolder = 49152;
    private const uint NodeColorHolder = 86016;

    // NetNode.FlagsLong bits that the game adds for each segment at a dead end, by the segment's direction.
    private const ulong EndForwardFlag = 0x800000000UL;
    private const ulong EndBackwardFlag = 0x1000000000UL;

    // NetInfo.ConnectGroup bits the game checks for direct-connect meshes at bends.
    private const int ConnectGroupMask = 268374015;
    private const int OneWayConnectGroups = 12288;
    private const int OneWayStartConnectGroup = 4096;
    private const int OneWayEndConnectGroup = 8192;

    /// <summary>Every vertex array, so that the game's functions find each array they write.</summary>
    public const RenderGroup.VertexArrays AllArrays = (RenderGroup.VertexArrays)0xFF;

    private static readonly MethodInfo? _getCenterPositions = typeof(NetNode).GetMethod(
        "GetCenterPositions",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new[] { typeof(ushort), typeof(Vector3[]), typeof(Vector3[]) },
        null);

    private static readonly MethodInfo? _refreshJunctionData = typeof(NetNode).GetMethod(
        "RefreshJunctionData",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new[] { typeof(ushort), typeof(int), typeof(ushort), typeof(Vector3), typeof(uint).MakeByRefType(), typeof(RenderManager.Instance).MakeByRefType() },
        null);

    /// <summary>Whether the private method that junction pieces need was found.</summary>
    public static bool CanDoJunctions => _getCenterPositions is not null;

    /// <summary>
    /// Computes the renderer's inputs for a junction's pieces, per segment slot, the way
    /// <c>NetNode.RefreshJunctionData</c> does before it draws the main meshes: centre positions from the
    /// renderer's own rule, then the game's private per-piece method into a local instance, so the renderer's
    /// data is untouched. The renderer calls that method on the main thread; here it runs on the simulation
    /// thread, which is fine because it only reads the network and writes into the instance it is given.
    /// Direct-connect pieces are left out. Slots without a segment are null.
    /// </summary>
    public static GpuParameters?[] ComputeGpuJunction(ushort nodeId)
    {
        var result = new GpuParameters?[8];
        if (_getCenterPositions is null || _refreshJunctionData is null)
        {
            return result;
        }

        NetSegment[] segments = Singleton<NetManager>.instance.m_segments.m_buffer;
        NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
        var deltaDirs = new Vector3[8];

        // The renderer computes these only for nodes with direct connections (tram or train tracks across the
        // junction, m_connectCount > 0). For other junctions it reads its shared buffer as the last such node
        // left it: zeros as long as no such node has been drawn, which is what this assumes.
        for (int i = 0; i < 8 && node.m_connectCount > 0; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            NetInfo info = segments[segmentId].Info;
            ItemClass connectionClass = info.GetConnectionClass();
            Vector3 direction = DirectionAt(segments[segmentId], nodeId);
            float maxDot = -1f;
            for (int j = 0; j < 8; j++)
            {
                ushort otherId = node.GetSegment(j);
                if (otherId == 0 || otherId == segmentId)
                {
                    continue;
                }

                NetInfo otherInfo = segments[otherId].Info;
                ItemClass otherClass = otherInfo.GetConnectionClass();
                if (!AreConnectedForCenter(info, connectionClass, otherInfo))
                {
                    continue;
                }

                // Unlike the level-of-detail code, the renderer only counts segments of the same service.
                if (connectionClass.m_service == otherClass.m_service)
                {
                    Vector3 otherDirection = DirectionAt(segments[otherId], nodeId);
                    maxDot = Mathf.Max(maxDot, (direction.x * otherDirection.x) + (direction.z * otherDirection.z));
                }
            }

            deltaDirs[i] = direction * Mathf.Max(2f + (maxDot * 2f), info.m_minCornerOffset * 0.4f);
        }

        var centers = new Vector3[8];
        _ = _getCenterPositions.Invoke(node, new object[] { nodeId, deltaDirs, centers });
        if (!node.Info.m_requireSegmentRenderers)
        {
            return result;
        }

        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            Vector3 center = centers[i];
            center.y = node.m_position.y + (node.m_heightOffset * 0.015625f);
            object[] arguments = { nodeId, i, segmentId, center, 0u, default(RenderManager.Instance) };
            _ = _refreshJunctionData.Invoke(node, arguments);
            result[i] = GpuParameters.FromInstance((RenderManager.Instance)arguments[5]);
        }

        return result;
    }

    /// <summary>
    /// Reads the renderer's instance data of a node's pieces, if the game has drawn the node recently: the
    /// chain of instances that starts at the node's entry in <c>RenderManager.m_indices</c>. Read without
    /// locking while the main thread may update it, which is good enough for a probe.
    /// </summary>
    public static List<GpuParameters> ReadRendererNodeData(ushort nodeId)
    {
        var pieces = new List<GpuParameters>();
        if (!Singleton<RenderManager>.exists)
        {
            return pieces;
        }

        RenderManager render = Singleton<RenderManager>.instance;
        uint holder = NodeColorHolder + nodeId;
        if (render.m_indices is null || holder >= render.m_indices.Length)
        {
            return pieces;
        }

        uint index = render.m_indices[holder];
        RenderManager.Instance[] instances = render.m_instances;
        while (index != ushort.MaxValue && index < instances.Length && pieces.Count < 16)
        {
            RenderManager.Instance data = instances[index];
            if (!data.m_initialized || (pieces.Count == 0 && data.m_holder != holder))
            {
                break;
            }

            pieces.Add(GpuParameters.FromInstance(data));
            index = data.m_nextInstance;
        }

        return pieces;
    }

    /// <summary>The pieces of a segment in a render layer, with their outputs.</summary>
    public static List<GroupPiece> ForSegment(ushort segmentId, int layer, Vector3 groupPosition)
    {
        NetManager net = Singleton<NetManager>.instance;
        NetSegment segment = net.m_segments.m_buffer[segmentId];
        NetNode[] nodes = net.m_nodes.m_buffer;
        NetInfo info = segment.Info;
        var pieces = new List<GroupPiece>();
        if (info.m_segments is null || info.m_segments.Length == 0 || (info.m_netLayers & (1 << layer)) == 0)
        {
            return pieces;
        }

        float vScale = info.m_netAI.GetVScale();

        // The order matters: the right-side calls set the smoothness flags last, as in the game.
        segment.CalculateCorner(segmentId, true, true, true, out Vector3 startLeft, out Vector3 startLeftDir, out bool smoothStart);
        segment.CalculateCorner(segmentId, true, false, true, out Vector3 endLeft, out Vector3 endLeftDir, out bool smoothEnd);
        segment.CalculateCorner(segmentId, true, true, false, out Vector3 startRight, out Vector3 startRightDir, out smoothStart);
        segment.CalculateCorner(segmentId, true, false, false, out Vector3 endRight, out Vector3 endRightDir, out smoothEnd);
        NetSegment.CalculateMiddlePoints(startLeft, startLeftDir, endRight, endRightDir, smoothStart, smoothEnd, out Vector3 leftMiddle1, out Vector3 leftMiddle2);
        NetSegment.CalculateMiddlePoints(startRight, startRightDir, endLeft, endLeftDir, smoothStart, smoothEnd, out Vector3 rightMiddle1, out Vector3 rightMiddle2);
        var meshScale = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 1f, 1f);
        Vector4 colorStart = RenderManager.GetColorLocation(SegmentColorHolder + segmentId);
        Vector4 colorEnd = colorStart;
        if (NetNode.BlendJunction(segment.m_startNode))
        {
            colorStart = RenderManager.GetColorLocation(NodeColorHolder + segment.m_startNode);
        }

        if (NetNode.BlendJunction(segment.m_endNode))
        {
            colorEnd = RenderManager.GetColorLocation(NodeColorHolder + segment.m_endNode);
        }

        var objectIndex = new Vector4(colorStart.x, colorStart.y, colorEnd.x, colorEnd.y);
        Vector3 middle = (nodes[segment.m_startNode].m_position + nodes[segment.m_endNode].m_position) * 0.5f;
        for (int i = 0; i < info.m_segments.Length; i++)
        {
            NetInfo.Segment mesh = info.m_segments[i];
            if (mesh.m_layer != layer || !mesh.CheckFlags(segment.m_flags, segment.m_flags2, out bool turnAround) || mesh.m_combinedLod is null)
            {
                continue;
            }

            Vector4 index = objectIndex;
            if (mesh.m_requireWindSpeed)
            {
                index.w = Singleton<WeatherManager>.instance.GetWindSpeed(middle);
            }
            else if (turnAround)
            {
                index = new Vector4(index.z, index.w, index.x, index.y);
            }

            var piece = new GroupPiece(GroupPieceKind.Segment, info, i)
            {
                SegmentMesh = mesh,
                SegmentId = segmentId,
                TurnAround = turnAround,
                MeshScale = meshScale,
                ObjectIndex = index,
                ColorIndex = info.m_netAI.GetObjectColorIndex(segmentId, ref segment),
            };
            if (turnAround)
            {
                piece.Left = NetSegment.CalculateControlMatrix(endLeft, rightMiddle2, rightMiddle1, startRight, endRight, leftMiddle2, leftMiddle1, startLeft, groupPosition, vScale);
                piece.Right = NetSegment.CalculateControlMatrix(endRight, leftMiddle2, leftMiddle1, startLeft, endLeft, rightMiddle2, rightMiddle1, startRight, groupPosition, vScale);
            }
            else
            {
                piece.Left = NetSegment.CalculateControlMatrix(startLeft, leftMiddle1, leftMiddle2, endRight, startRight, rightMiddle1, rightMiddle2, endLeft, groupPosition, vScale);
                piece.Right = NetSegment.CalculateControlMatrix(startRight, rightMiddle1, rightMiddle2, endLeft, startLeft, leftMiddle1, leftMiddle2, endRight, groupPosition, vScale);
            }

            RunSegmentFunction(piece, groupPosition);
            RunOnMainMesh(piece, groupPosition);
            pieces.Add(piece);
        }

        return pieces;
    }

    /// <summary>
    /// The pieces of a node in a render layer, with their outputs: junction pieces, a dead end's cap, or a
    /// bend's segment and direct-connect meshes. Nodes of other kinds have none.
    /// </summary>
    public static List<GroupPiece> ForNode(ushort nodeId, int layer, Vector3 groupPosition, NodeDetails details)
    {
        NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
        if ((node.m_flags & NetNode.Flags.Junction) != 0)
        {
            return ForJunction(nodeId, node, layer, groupPosition, details);
        }

        var pieces = new List<GroupPiece>();
        NetInfo info = node.Info;
        if ((info.m_netLayers & (1 << layer)) == 0)
        {
            return pieces;
        }

        if ((node.m_flags & NetNode.Flags.End) != 0)
        {
            AddEnd(nodeId, node, info, layer, groupPosition, pieces);
        }
        else if ((node.m_flags & NetNode.Flags.Bend) != 0)
        {
            AddBend(nodeId, node, info, layer, groupPosition, pieces);
        }

        return pieces;
    }

    private static List<GroupPiece> ForJunction(ushort nodeId, NetNode node, int layer, Vector3 groupPosition, NodeDetails details)
    {
        var pieces = new List<GroupPiece>();
        if (_getCenterPositions is null)
        {
            throw new InvalidOperationException("NetNode.GetCenterPositions wasn't found; the game has changed.");
        }

        NetSegment[] segments = Singleton<NetManager>.instance.m_segments.m_buffer;
        NetInfo nodeInfo = node.Info;
        int layerBit = 1 << layer;

        // How far each segment's share of the centre is pushed along its direction: further when another
        // segment continues it in a straight line.
        var deltaDirs = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            NetInfo info = segments[segmentId].Info;
            ItemClass connectionClass = info.GetConnectionClass();
            Vector3 direction = DirectionAt(segments[segmentId], nodeId);
            float maxDot = -1f;
            for (int j = 0; j < 8; j++)
            {
                ushort otherId = node.GetSegment(j);
                if (otherId == 0 || otherId == segmentId)
                {
                    continue;
                }

                NetInfo otherInfo = segments[otherId].Info;
                if (((nodeInfo.m_netLayers | info.m_netLayers | otherInfo.m_netLayers) & layerBit) == 0
                    || !AreConnectedForCenter(info, connectionClass, otherInfo))
                {
                    continue;
                }

                Vector3 otherDirection = DirectionAt(segments[otherId], nodeId);
                maxDot = Mathf.Max(maxDot, (direction.x * otherDirection.x) + (direction.z * otherDirection.z));
            }

            deltaDirs[i] = direction * (2f + (maxDot * 2f));
        }

        var centers = new Vector3[8];
        object boxedNode = node;
        _ = _getCenterPositions.Invoke(boxedNode, new object[] { nodeId, deltaDirs, centers });
        for (int i = 0; i < 8; i++)
        {
            centers[i].y = node.m_position.y + (node.m_heightOffset * 0.015625f);
        }

        details.DeltaDirections = deltaDirs;
        details.CenterPositions = centers;
        if ((nodeInfo.m_netLayers & layerBit) == 0 || !nodeInfo.m_requireSegmentRenderers)
        {
            return pieces;
        }

        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            NetSegment segment = segments[segmentId];
            NetInfo info = segment.Info;
            if (info.m_nodes is null || info.m_nodes.Length == 0)
            {
                continue;
            }

            AddJunctionPieces(nodeId, node, nodeInfo, i, segmentId, segment, info, segments, centers, layer, groupPosition, pieces);
        }

        GpuParameters?[] gpu = ComputeGpuJunction(nodeId);
        foreach (GroupPiece piece in pieces)
        {
            if (piece.Slot >= 0 && gpu[piece.Slot] is GpuParameters parameters)
            {
                piece.Gpu = new GpuParameters
                {
                    Position = parameters.Position,
                    Left = parameters.Left,
                    Right = parameters.Right,
                    LeftB = parameters.LeftB,
                    RightB = parameters.RightB,
                    MeshScale = parameters.MeshScale,
                    CenterPos = parameters.CenterPos,
                    SideScale = parameters.SideScale,
                };
                RunGpuOnMainMesh(piece);
            }
        }

        return pieces;
    }

    private static void AddJunctionPieces(
        ushort nodeId,
        NetNode node,
        NetInfo nodeInfo,
        int index,
        ushort segmentId,
        NetSegment segment,
        NetInfo info,
        NetSegment[] segments,
        Vector3[] centers,
        int layer,
        Vector3 groupPosition,
        List<GroupPiece> pieces)
    {
        float vScale = info.m_netAI.GetVScale();
        ItemClass connectionClass = info.GetConnectionClass();
        Vector3 direction = DirectionAt(segment, nodeId);

        // The neighbours on either side: the segments with the smallest turn to the left and to the right.
        float leftScore = -4f;
        float rightScore = -4f;
        ushort leftId = 0;
        ushort rightId = 0;
        for (int j = 0; j < 8; j++)
        {
            ushort otherId = node.GetSegment(j);
            if (otherId == 0 || otherId == segmentId)
            {
                continue;
            }

            NetInfo otherInfo = segments[otherId].Info;
            ItemClass otherClass = otherInfo.GetConnectionClass();
            bool connected = connectionClass.m_service == otherClass.m_service
                && (!(info.m_onlySameConnectionGroup || otherInfo.m_onlySameConnectionGroup) || (info.m_connectGroup & otherInfo.m_connectGroup) != 0);
            if (!connected)
            {
                continue;
            }

            Vector3 otherDirection = DirectionAt(segments[otherId], nodeId);
            float dot = (direction.x * otherDirection.x) + (direction.z * otherDirection.z);
            float cross = (otherDirection.z * direction.x) - (otherDirection.x * direction.z);
            if (cross < 0f)
            {
                if (dot > leftScore)
                {
                    leftScore = dot;
                    leftId = otherId;
                }

                dot = -2f - dot;
                if (dot > rightScore)
                {
                    rightScore = dot;
                    rightId = otherId;
                }
            }
            else
            {
                if (dot > rightScore)
                {
                    rightScore = dot;
                    rightId = otherId;
                }

                dot = -2f - dot;
                if (dot > leftScore)
                {
                    leftScore = dot;
                    leftId = otherId;
                }
            }
        }

        bool start = segment.m_startNode == nodeId;
        segment.CalculateCorner(segmentId, true, start, false, out Vector3 corner0, out Vector3 cornerDir0, out _);
        segment.CalculateCorner(segmentId, true, start, true, out Vector3 corner1, out Vector3 cornerDir1, out _);
        float pavementRatio = info.m_pavementWidth / info.m_halfWidth * 0.5f;
        var meshScale = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 0.5f - pavementRatio, pavementRatio);
        Vector3 center = centers[index];
        Matrix4x4 left;
        Matrix4x4 right;
        Matrix4x4 leftB;
        Matrix4x4 rightB;
        Vector4 sideScale;
        if (leftId != 0 && rightId != 0)
        {
            NetSegment leftSegment = segments[leftId];
            NetInfo leftInfo = leftSegment.Info;
            bool leftStart = leftSegment.m_startNode == nodeId;
            leftSegment.CalculateCorner(leftId, true, leftStart, true, out Vector3 leftCorner0, out Vector3 leftDir0, out _);
            leftSegment.CalculateCorner(leftId, true, leftStart, false, out Vector3 leftCorner1, out Vector3 leftDir1, out _);
            float leftPavement = (pavementRatio + (leftInfo.m_pavementWidth / leftInfo.m_halfWidth * 0.5f)) * 0.5f;
            float leftWidth = 2f * info.m_halfWidth / (info.m_halfWidth + leftInfo.m_halfWidth);

            NetSegment rightSegment = segments[rightId];
            NetInfo rightInfo = rightSegment.Info;
            bool rightStart = rightSegment.m_startNode == nodeId;
            rightSegment.CalculateCorner(rightId, true, rightStart, true, out Vector3 rightCorner0, out Vector3 rightDir0, out _);
            rightSegment.CalculateCorner(rightId, true, rightStart, false, out Vector3 rightCorner1, out Vector3 rightDir1, out _);
            float rightPavement = (pavementRatio + (rightInfo.m_pavementWidth / rightInfo.m_halfWidth * 0.5f)) * 0.5f;
            float rightWidth = 2f * info.m_halfWidth / (info.m_halfWidth + rightInfo.m_halfWidth);

            NetSegment.CalculateMiddlePoints(corner0, -cornerDir0, leftCorner0, -leftDir0, true, true, out Vector3 a1, out Vector3 a2);
            NetSegment.CalculateMiddlePoints(corner1, -cornerDir1, leftCorner1, -leftDir1, true, true, out Vector3 b1, out Vector3 b2);
            NetSegment.CalculateMiddlePoints(corner0, -cornerDir0, rightCorner0, -rightDir0, true, true, out Vector3 c1, out Vector3 c2);
            NetSegment.CalculateMiddlePoints(corner1, -cornerDir1, rightCorner1, -rightDir1, true, true, out Vector3 d1, out Vector3 d2);
            left = NetSegment.CalculateControlMatrix(corner0, a1, a2, leftCorner0, corner0, a1, a2, leftCorner0, groupPosition, vScale);
            right = NetSegment.CalculateControlMatrix(corner1, b1, b2, leftCorner1, corner1, b1, b2, leftCorner1, groupPosition, vScale);
            leftB = NetSegment.CalculateControlMatrix(corner0, c1, c2, rightCorner0, corner0, c1, c2, rightCorner0, groupPosition, vScale);
            rightB = NetSegment.CalculateControlMatrix(corner1, d1, d2, rightCorner1, corner1, d1, d2, rightCorner1, groupPosition, vScale);
            sideScale = new Vector4(leftPavement, leftWidth, rightPavement, rightWidth);
        }
        else
        {
            // No neighbour of the same kind: the piece is drawn like a dead end, around the segment's own corners.
            center.x = (corner0.x + corner1.x) * 0.5f;
            center.z = (corner0.z + corner1.z) * 0.5f;
            centers[index] = center;
            CapMatrices(corner0, cornerDir0, corner1, cornerDir1, nodeInfo.m_netAI.GetEndRadius() * 1.3333334f, groupPosition, vScale, out left, out right, out leftB, out rightB);
            sideScale = new Vector4(pavementRatio, 1f, pavementRatio, 1f);
        }

        Vector4 centerPos = center - groupPosition;
        centerPos.w = (left.m33 + right.m33 + leftB.m33 + rightB.m33) * 0.25f;
        Vector4 colorA;
        Vector4 colorB;
        if (NetNode.BlendJunction(nodeId))
        {
            colorA = RenderManager.GetColorLocation(NodeColorHolder + nodeId);
            colorB = colorA;
        }
        else
        {
            colorA = RenderManager.GetColorLocation(SegmentColorHolder + segmentId);
            colorB = RenderManager.GetColorLocation(NodeColorHolder + nodeId);
        }

        var objectIndex = new Vector4(colorA.x, colorA.y, colorB.x, colorB.y);
        for (int i = 0; i < info.m_nodes.Length; i++)
        {
            NetInfo.Node mesh = info.m_nodes[i];
            if (mesh.m_layer != layer || mesh.m_directConnect || mesh.m_combinedLod is null
                || !mesh.CheckFlags(node.flags) || !mesh.CheckTags(nodeId, ref node))
            {
                continue;
            }

            var piece = new GroupPiece(GroupPieceKind.Junction, info, i)
            {
                NodeMesh = mesh,
                SegmentId = segmentId,
                Slot = index,
                Left = left,
                Right = right,
                LeftB = leftB,
                RightB = rightB,
                MeshScale = meshScale,
                CenterPos = centerPos,
                SideScale = sideScale,
                ObjectIndex = WithWind(objectIndex, mesh.m_requireWindSpeed, node.m_position),
                ColorIndex = nodeInfo.m_netAI.GetObjectColorIndex(nodeId, ref node),
            };
            RunNodeFunction(piece);
            RunOnMainMesh(piece, groupPosition);
            pieces.Add(piece);
        }
    }

    private static void AddEnd(ushort nodeId, NetNode node, NetInfo info, int layer, Vector3 groupPosition, List<GroupPiece> pieces)
    {
        if (info.m_nodes is null || info.m_nodes.Length == 0)
        {
            return;
        }

        NetSegment[] segments = Singleton<NetManager>.instance.m_segments.m_buffer;
        float vScale = info.m_netAI.GetVScale() / 1.5f;
        Vector3 corner0 = Vector3.zero;
        Vector3 cornerDir0 = Vector3.zero;
        Vector3 corner1 = Vector3.zero;
        Vector3 cornerDir1 = Vector3.zero;
        ushort segmentId = 0;
        for (int i = 0; i < 8; i++)
        {
            ushort id = node.GetSegment(i);
            if (id == 0)
            {
                continue;
            }

            NetSegment segment = segments[id];
            bool start = segment.m_startNode == nodeId;
            segment.CalculateCorner(id, true, start, false, out corner0, out cornerDir0, out _);
            segment.CalculateCorner(id, true, start, true, out corner1, out cornerDir1, out _);
            segmentId = id;
        }

        CapMatrices(corner0, cornerDir0, corner1, cornerDir1, info.m_netAI.GetEndRadius() * 1.3333334f, groupPosition, vScale, out Matrix4x4 left, out Matrix4x4 right, out Matrix4x4 leftB, out Matrix4x4 rightB);
        float pavementRatio = info.m_pavementWidth / info.m_halfWidth * 0.5f;
        var meshScale = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 0.5f - pavementRatio, pavementRatio);
        var centerPos = new Vector4(
            node.m_position.x - groupPosition.x,
            node.m_position.y - groupPosition.y + (node.m_heightOffset * 0.015625f),
            node.m_position.z - groupPosition.z,
            0f);
        centerPos.w = (left.m33 + right.m33 + leftB.m33 + rightB.m33) * 0.25f;
        var sideScale = new Vector4(pavementRatio, 1f, pavementRatio, 1f);
        Vector4 color = RenderManager.GetColorLocation(SegmentColorHolder + segmentId);
        var objectIndex = new Vector4(color.x, color.y, color.x, color.y);

        // The game adds a flag per segment saying whether it points into or out of the dead end.
        ulong flags = (ulong)node.flags;
        for (int i = 0; i < 8; i++)
        {
            ushort id = node.GetSegment(i);
            if (id == 0)
            {
                continue;
            }

            bool inverted = (segments[id].m_flags & NetSegment.Flags.Invert) != 0;
            if (segments[id].m_startNode == nodeId)
            {
                flags |= inverted ? EndBackwardFlag : EndForwardFlag;
            }
            else if (segments[id].m_endNode == nodeId)
            {
                flags |= inverted ? EndForwardFlag : EndBackwardFlag;
            }
        }

        for (int i = 0; i < info.m_nodes.Length; i++)
        {
            NetInfo.Node mesh = info.m_nodes[i];
            if (mesh.m_layer != layer || !mesh.CheckFlags((NetNode.FlagsLong)flags) || mesh.m_combinedLod is null || mesh.m_directConnect)
            {
                continue;
            }

            var piece = new GroupPiece(GroupPieceKind.End, info, i)
            {
                NodeMesh = mesh,
                SegmentId = segmentId,
                Left = left,
                Right = right,
                LeftB = leftB,
                RightB = rightB,
                MeshScale = meshScale,
                CenterPos = centerPos,
                SideScale = sideScale,
                ObjectIndex = WithWind(objectIndex, mesh.m_requireWindSpeed, node.m_position),
                ColorIndex = info.m_netAI.GetObjectColorIndex(nodeId, ref node),
            };
            RunNodeFunction(piece);
            RunOnMainMesh(piece, groupPosition);
            pieces.Add(piece);
        }
    }

    private static void AddBend(ushort nodeId, NetNode node, NetInfo info, int layer, Vector3 groupPosition, List<GroupPiece> pieces)
    {
        NetInfo.Segment[] segmentMeshes = info.m_segments ?? new NetInfo.Segment[0];
        NetInfo.Node[] nodeMeshes = info.m_nodes ?? new NetInfo.Node[0];
        bool hasSegments = segmentMeshes.Length != 0;
        bool hasNodes = nodeMeshes.Length != 0;
        if (!hasSegments && !hasNodes)
        {
            return;
        }

        NetSegment[] segments = Singleton<NetManager>.instance.m_segments.m_buffer;
        float vScale = info.m_netAI.GetVScale();
        Vector3 cornerA0 = Vector3.zero;
        Vector3 dirA0 = Vector3.zero;
        Vector3 cornerA1 = Vector3.zero;
        Vector3 dirA1 = Vector3.zero;
        Vector3 cornerB0 = Vector3.zero;
        Vector3 dirB0 = Vector3.zero;
        Vector3 cornerB1 = Vector3.zero;
        Vector3 dirB1 = Vector3.zero;
        ushort segmentA = 0;
        ushort segmentB = 0;
        bool haveA = false;
        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            ushort id = node.GetSegment(i);
            if (id == 0)
            {
                continue;
            }

            NetSegment segment = segments[id];
            bool first = ++count == 1;
            bool start = segment.m_startNode == nodeId;

            // Segment A is preferably the one that ends here.
            if ((first && !start) || (!first && !haveA))
            {
                segment.CalculateCorner(id, true, start, false, out cornerA0, out dirA0, out _);
                segment.CalculateCorner(id, true, start, true, out cornerA1, out dirA1, out _);
                haveA = true;
                segmentA = id;
            }
            else
            {
                segment.CalculateCorner(id, true, start, true, out cornerB0, out dirB0, out _);
                segment.CalculateCorner(id, true, start, false, out cornerB1, out dirB1, out _);
                segmentB = id;
            }
        }

        NetSegment.CalculateMiddlePoints(cornerA0, -dirA0, cornerB0, -dirB0, true, true, out Vector3 a1, out Vector3 a2);
        NetSegment.CalculateMiddlePoints(cornerA1, -dirA1, cornerB1, -dirB1, true, true, out Vector3 b1, out Vector3 b2);
        Matrix4x4 left = NetSegment.CalculateControlMatrix(cornerA0, a1, a2, cornerB0, cornerA1, b1, b2, cornerB1, groupPosition, vScale);
        Matrix4x4 right = NetSegment.CalculateControlMatrix(cornerA1, b1, b2, cornerB1, cornerA0, a1, a2, cornerB0, groupPosition, vScale);
        var meshScale = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 1f, 1f);
        Vector4 color = RenderManager.GetColorLocation(NodeColorHolder + nodeId);
        var objectIndex = new Vector4(color.x, color.y, color.x, color.y);
        if (hasSegments)
        {
            NetSegment segmentACopy = segments[segmentA];
            NetSegment.Flags bendFlags = info.m_netAI.GetBendFlags(nodeId, ref node);
            NetSegment.Flags2 eventFlags = info.m_netAI.GetEventFlags(nodeId, ref node);
            for (int i = 0; i < segmentMeshes.Length; i++)
            {
                NetInfo.Segment mesh = segmentMeshes[i];
                if (mesh.m_layer != layer || !mesh.CheckFlags(bendFlags, eventFlags, out bool turnAround)
                    || mesh.m_combinedLod is null || mesh.m_disableBendNodes)
                {
                    continue;
                }

                Vector4 scale = meshScale;
                if (turnAround)
                {
                    scale.x = -scale.x;
                    scale.y = -scale.y;
                }

                var piece = new GroupPiece(GroupPieceKind.BendSegment, info, i)
                {
                    SegmentMesh = mesh,
                    SegmentId = segmentA,
                    TurnAround = turnAround,
                    Left = left,
                    Right = right,
                    MeshScale = scale,
                    ObjectIndex = WithWind(objectIndex, mesh.m_requireWindSpeed, node.m_position),
                    ColorIndex = info.m_netAI.GetObjectColorIndex(segmentA, ref segmentACopy),
                };
                RunSegmentFunction(piece, groupPosition);
                RunOnMainMesh(piece, groupPosition);
                pieces.Add(piece);
            }
        }

        if (!hasNodes)
        {
            return;
        }

        for (int i = 0; i < nodeMeshes.Length; i++)
        {
            NetInfo.Node mesh = nodeMeshes[i];
            int connectGroup = (int)mesh.m_connectGroup;
            if ((connectGroup != 0 && (connectGroup & (int)info.m_connectGroup & ConnectGroupMask) == 0)
                || mesh.m_layer != layer || !mesh.CheckFlags(node.flags) || mesh.m_combinedLod is null || !mesh.m_directConnect)
            {
                continue;
            }

            Vector4 scale = meshScale;
            if ((connectGroup & OneWayConnectGroups) != 0)
            {
                bool forwardA = (segments[segmentA].m_startNode == nodeId) == ((segments[segmentA].m_flags & NetSegment.Flags.Invert) == 0);
                bool forwardB = (segments[segmentB].m_startNode == nodeId) == ((segments[segmentB].m_flags & NetSegment.Flags.Invert) == 0);
                if (forwardA == forwardB)
                {
                    continue;
                }

                if (forwardA)
                {
                    if ((connectGroup & OneWayStartConnectGroup) == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    if ((connectGroup & OneWayEndConnectGroup) == 0)
                    {
                        continue;
                    }

                    scale.x = -scale.x;
                    scale.y = -scale.y;
                }
            }

            var piece = new GroupPiece(GroupPieceKind.BendNode, info, i)
            {
                NodeMesh = mesh,
                SegmentId = segmentA,
                Left = left,
                Right = right,
                MeshScale = scale,
                ObjectIndex = WithWind(objectIndex, mesh.m_requireWindSpeed, node.m_position),
                ColorIndex = info.m_netAI.GetObjectColorIndex(nodeId, ref node),
            };
            RunBendNodeFunction(piece);
            RunOnMainMesh(piece, groupPosition);
            pieces.Add(piece);
        }
    }

    // The four curves of a cap around a segment's two corners: out of the node and back. The game draws
    // dead ends and junction pieces without neighbours this way.
    private static void CapMatrices(
        Vector3 corner0,
        Vector3 dir0,
        Vector3 corner1,
        Vector3 dir1,
        float radius,
        Vector3 groupPosition,
        float vScale,
        out Matrix4x4 left,
        out Matrix4x4 right,
        out Matrix4x4 leftB,
        out Matrix4x4 rightB)
    {
        Vector3 out0 = corner0 - (dir0 * radius);
        Vector3 out1 = corner1 - (dir1 * radius);
        Vector3 in0 = corner0 + (dir0 * radius);
        Vector3 in1 = corner1 + (dir1 * radius);
        left = NetSegment.CalculateControlMatrix(corner0, out0, out1, corner1, corner0, out0, out1, corner1, groupPosition, vScale);
        right = NetSegment.CalculateControlMatrix(corner1, in1, in0, corner0, corner1, in1, in0, corner0, groupPosition, vScale);
        leftB = NetSegment.CalculateControlMatrix(corner0, in0, in1, corner1, corner0, in0, in1, corner1, groupPosition, vScale);
        rightB = NetSegment.CalculateControlMatrix(corner1, out1, out0, corner0, corner1, out1, out0, corner0, groupPosition, vScale);
        var offset = new Vector4(0.2f, 0.2f, 0.2f, 0.2f);
        left.SetRow(3, left.GetRow(3) + offset);
        right.SetRow(3, right.GetRow(3) + offset);
        leftB.SetRow(3, leftB.GetRow(3) + offset);
        rightB.SetRow(3, rightB.GetRow(3) + offset);
    }

    private static Vector3 DirectionAt(NetSegment segment, ushort nodeId) =>
        segment.m_startNode == nodeId ? segment.m_startDirection : segment.m_endDirection;

    private static bool AreConnectedForCenter(NetInfo info, ItemClass connectionClass, NetInfo otherInfo)
    {
        ItemClass otherClass = otherInfo.GetConnectionClass();
        bool connected = connectionClass.m_service == otherClass.m_service
            && (!(info.m_onlySameConnectionGroup || otherInfo.m_onlySameConnectionGroup) || (info.m_connectGroup & otherInfo.m_connectGroup) != 0);
        return connected
            || (info.m_nodeConnectGroups & otherInfo.m_connectGroup) != 0
            || (otherInfo.m_nodeConnectGroups & info.m_connectGroup) != 0;
    }

    private static Vector4 WithWind(Vector4 objectIndex, bool requireWindSpeed, Vector3 position)
    {
        if (requireWindSpeed)
        {
            objectIndex.w = Singleton<WeatherManager>.instance.GetWindSpeed(position);
        }

        return objectIndex;
    }

    private static RenderGroup.MeshData NewOutput(RenderGroup.MeshData source, GroupPiece piece)
    {
        piece.OutputVertexCount = source.m_vertices.Length;
        piece.OutputTriangleCount = source.m_triangles.Length;
        return new RenderGroup.MeshData(AllArrays, piece.OutputVertexCount, piece.OutputTriangleCount);
    }

    /// <summary>
    /// Runs the piece's bending function on its main mesh, through a stand-in prefab mesh that only the probe
    /// knows: a new <c>NetInfo.Segment</c> or <c>NetInfo.Node</c> whose combined level-of-detail data is a copy
    /// of the main mesh. The game's function reads only that data and the flags copied here
    /// (docs/research/cs1-networks.md, Bending network meshes). Nothing in the game refers to the
    /// stand-in, so the game's state is unchanged.
    /// </summary>
    private static void RunOnMainMesh(GroupPiece piece, Vector3 groupPosition)
    {
        GroupPiece? standIn = BendMainMesh(piece, piece, groupPosition);
        if (standIn is not null)
        {
            piece.MainOutput = standIn.Output;
            piece.MainOutputVertexCount = standIn.OutputVertexCount;
            piece.MainOutputTriangleCount = standIn.OutputTriangleCount;
        }
    }

    /// <summary>Bends a junction piece's main mesh with the renderer's inputs (<see cref="GroupPiece.Gpu"/>).</summary>
    private static void RunGpuOnMainMesh(GroupPiece piece)
    {
        GpuParameters gpu = piece.Gpu!;
        var parameters = new GroupPiece(piece.Kind, piece.Info, piece.MeshIndex)
        {
            Left = gpu.Left,
            Right = gpu.Right,
            LeftB = gpu.LeftB,
            RightB = gpu.RightB,
            MeshScale = gpu.MeshScale,
            CenterPos = gpu.CenterPos,
            SideScale = gpu.SideScale,
            ObjectIndex = piece.ObjectIndex,
            ColorIndex = piece.ColorIndex,
        };
        GroupPiece? standIn = BendMainMesh(piece, parameters, gpu.Position);
        if (standIn is not null)
        {
            gpu.MainOutput = standIn.Output;
            gpu.MainOutputVertexCount = standIn.OutputVertexCount;
            gpu.MainOutputTriangleCount = standIn.OutputTriangleCount;
        }
    }

    // Returns the stand-in piece with its output, or null (with a note on the piece) if the main mesh can't be read.
    private static GroupPiece? BendMainMesh(GroupPiece piece, GroupPiece parameters, Vector3 groupPosition)
    {
        Mesh? mesh = piece.MainMesh;
        if (mesh == null)
        {
            piece.MainOutputNote = "no main mesh";
            return null;
        }

        if (!mesh.isReadable)
        {
            piece.MainOutputNote = "the main mesh isn't readable";
            return null;
        }

        RenderGroup.MeshData source = CopyMesh(mesh);
        var lod = new NetInfo.LodValue { m_key = new NetInfo.LodKey { m_mesh = new NetInfo.MeshValue { m_data = source } } };
        var standIn = new GroupPiece(piece.Kind, piece.Info, piece.MeshIndex)
        {
            Left = parameters.Left,
            Right = parameters.Right,
            LeftB = parameters.LeftB,
            RightB = parameters.RightB,
            MeshScale = parameters.MeshScale,
            CenterPos = parameters.CenterPos,
            SideScale = parameters.SideScale,
            ObjectIndex = parameters.ObjectIndex,
            ColorIndex = parameters.ColorIndex,
        };
        if (piece.SegmentMesh is not null)
        {
            standIn.SegmentMesh = new NetInfo.Segment
            {
                m_combinedLod = lod,
                m_requireSurfaceMaps = piece.SegmentMesh.m_requireSurfaceMaps,
                m_requireHeightMap = piece.SegmentMesh.m_requireHeightMap,
                m_layer = piece.SegmentMesh.m_layer,
            };
            RunSegmentFunction(standIn, groupPosition);
        }
        else if (piece.NodeMesh is not null)
        {
            standIn.NodeMesh = new NetInfo.Node
            {
                m_combinedLod = lod,
                m_requireSurfaceMaps = piece.NodeMesh.m_requireSurfaceMaps,
                m_layer = piece.NodeMesh.m_layer,
            };
            if (piece.Kind == GroupPieceKind.BendNode)
            {
                RunBendNodeFunction(standIn);
            }
            else
            {
                RunNodeFunction(standIn);
            }
        }

        return standIn;
    }

    // All submeshes' triangles, one after the other.
    private static RenderGroup.MeshData CopyMesh(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        var triangles = new List<int>();
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            triangles.AddRange(mesh.GetTriangles(i));
        }

        var data = new RenderGroup.MeshData(AllArrays, vertices.Length, triangles.Count);
        Array.Copy(vertices, data.m_vertices, vertices.Length);
        _ = CopyIfComplete(mesh.normals, data.m_normals);
        _ = CopyIfComplete(mesh.tangents, data.m_tangents);
        _ = CopyIfComplete(mesh.uv, data.m_uvs);
        if (!CopyIfComplete(mesh.colors32, data.m_colors))
        {
            // The game treats mesh data without colors as white; the stand-in always has a color array.
            for (int i = 0; i < data.m_colors.Length; i++)
            {
                data.m_colors[i] = new Color32(255, 255, 255, 255);
            }
        }

        triangles.CopyTo(data.m_triangles);
        return data;
    }

    // A mesh without an array returns an empty one; then the game's defaults apply, as for the real data.
    private static bool CopyIfComplete<T>(T[] values, T[] target)
    {
        if (values.Length != target.Length)
        {
            return false;
        }

        Array.Copy(values, target, values.Length);
        return true;
    }

    private static void RunSegmentFunction(GroupPiece piece, Vector3 groupPosition)
    {
        RenderGroup.MeshData data = NewOutput(piece.SourceData!, piece);
        int vertexIndex = 0;
        int triangleIndex = 0;
        bool requireSurfaceMaps = false;
        NetSegment.PopulateGroupData(
            piece.Info, piece.SegmentMesh, piece.Left, piece.Right, piece.MeshScale, piece.ObjectIndex,
            ref vertexIndex, ref triangleIndex, groupPosition, data, ref requireSurfaceMaps, piece.ColorIndex);
        piece.Output = data;
    }

    private static void RunNodeFunction(GroupPiece piece)
    {
        RenderGroup.MeshData data = NewOutput(piece.SourceData!, piece);
        int vertexIndex = 0;
        int triangleIndex = 0;
        bool requireSurfaceMaps = false;
        NetNode.PopulateGroupData(
            piece.Info, piece.NodeMesh, piece.Left, piece.Right, piece.LeftB, piece.RightB, piece.MeshScale,
            piece.CenterPos, piece.SideScale, piece.ObjectIndex, ref vertexIndex, ref triangleIndex, data,
            ref requireSurfaceMaps, piece.ColorIndex);
        piece.Output = data;
    }

    private static void RunBendNodeFunction(GroupPiece piece)
    {
        RenderGroup.MeshData data = NewOutput(piece.SourceData!, piece);
        int vertexIndex = 0;
        int triangleIndex = 0;
        bool requireSurfaceMaps = false;
        NetNode.PopulateGroupData(
            piece.Info, piece.NodeMesh, piece.Left, piece.Right, piece.MeshScale, piece.ObjectIndex,
            ref vertexIndex, ref triangleIndex, data, ref requireSurfaceMaps, piece.ColorIndex);
        piece.Output = data;
    }
}
