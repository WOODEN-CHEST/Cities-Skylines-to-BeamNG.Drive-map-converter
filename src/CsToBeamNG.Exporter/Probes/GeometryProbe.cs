using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ColossalFramework;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Probes;

/// <summary>
/// The geometry probe, a developer tool for research (plan step 0.7): for a few automatically chosen road
/// segments and nodes it writes the prefab meshes, the inputs of the game's bending functions, and the
/// game's own bent output, so the converter can check its reimplementation
/// (docs/research/cs1-networks.md, Bending network meshes). It runs on the simulation thread,
/// inside an export.
/// </summary>
internal static class GeometryProbe
{
    /// <summary>The probe file's format name and version, checked by the converter's reader.</summary>
    public const string FormatName = "cstobeamng-geometry-probe";

    public const int FormatVersion = 1;

    /// <summary>Writes the probe into a folder and returns a one-line summary.</summary>
    /// <param name="folder">The probe's folder; created if needed.</param>
    /// <param name="log">The export's log.</param>
    public static string Write(string folder, ExportLog log)
    {
        if (!Singleton<NetManager>.exists)
        {
            return "skipped: there is no network";
        }

        List<ProbeTarget> targets = ChooseTargets();
        var registry = new ProbeMeshRegistry();
        var results = new List<ProbeResult>();
        int pieceCount = 0;
        int failures = 0;
        foreach (ProbeTarget target in targets)
        {
            log.Detail("Geometry probe: " + target.Label + " " + (target.IsNode ? "node " : "segment ") + target.Id.ToString(CultureInfo.InvariantCulture) + " (" + target.Reason + ")");
            ProbeResult result;
            try
            {
                result = Probe(target, registry);
                foreach (ProbeLayer layer in result.Layers)
                {
                    pieceCount += layer.Pieces.Count;
                }
            }
            catch (Exception e)
            {
                failures++;
                log.Warning("Geometry probe: " + target.Label + " failed: " + e);
                result = new ProbeResult(target) { Error = e.ToString() };
            }

            results.Add(result);
        }

        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, DumpProbes.GeometryFileName);
        using (JsonWriter json = JsonWriter.CreateFile(path))
        {
            WriteFile(json, results, registry);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} objects, {1} pieces, {2} meshes{3}, written to {4}",
            results.Count,
            pieceCount,
            registry.Meshes.Count,
            failures == 0 ? string.Empty : ", " + failures.ToString(CultureInfo.InvariantCulture) + " objects failed",
            path);
    }

    private static List<ProbeTarget> ChooseTargets()
    {
        NetManager net = Singleton<NetManager>.instance;
        NetSegment[] segments = net.m_segments.m_buffer;
        NetNode[] nodes = net.m_nodes.m_buffer;
        var best = new Dictionary<string, ProbeTarget>();

        for (int i = 1; i < segments.Length; i++)
        {
            NetSegment segment = segments[i];
            if (!IsUsableSegment(segment))
            {
                continue;
            }

            ushort id = (ushort)i;
            Vector3 start = nodes[segment.m_startNode].m_position;
            Vector3 end = nodes[segment.m_endNode].m_position;
            float length = segment.m_averageLength;
            Vector2 startDir = new Vector2(segment.m_startDirection.x, segment.m_startDirection.z).normalized;
            Vector2 endDir = new Vector2(segment.m_endDirection.x, segment.m_endDirection.z).normalized;
            float turn = Mathf.Acos(Mathf.Clamp(-Vector2.Dot(startDir, endDir), -1f, 1f));
            float rise = Mathf.Abs(end.y - start.y);
            if (turn < 0.01f && rise < 0.05f && length >= 40f)
            {
                Offer(best, "straight-segment", false, id, length, "straight and level, " + Metres(length));
            }

            if (length >= 30f)
            {
                Offer(best, "curved-segment", false, id, turn, "turns " + Degrees(turn) + " over " + Metres(length));
            }

            if (length >= 30f && turn < 0.1f)
            {
                Offer(best, "sloped-segment", false, id, rise / length, "rises " + Metres(rise) + " over " + Metres(length));
            }

            if (HasTurnedMesh(segment))
            {
                Offer(best, "turned-segment", false, id, -i, "a mesh drawn turned around");
            }
        }

        for (int i = 1; i < nodes.Length; i++)
        {
            NetNode node = nodes[i];
            if (!IsUsableNode(node, segments, out int count, out bool mixedWidths))
            {
                continue;
            }

            ushort id = (ushort)i;
            if ((node.m_flags & NetNode.Flags.Junction) != 0)
            {
                if (count >= 3 && NetGroupPieces.ReadRendererNodeData(id).Count >= count)
                {
                    Offer(best, "junction-rendered", true, id, -i, count.ToString(CultureInfo.InvariantCulture) + " segments, drawn by the game recently");
                }

                // The lowest id wins among equals, so the choice is stable for a save.
                if (count == 4)
                {
                    Offer(best, "junction-4way", true, id, -i, "4 segments");
                }
                else if (count == 3)
                {
                    Offer(best, "junction-t", true, id, -i, "3 segments");
                }

                if (mixedWidths)
                {
                    Offer(best, "junction-mixed-widths", true, id, -i, count.ToString(CultureInfo.InvariantCulture) + " segments of different widths");
                }
            }
            else if ((node.m_flags & NetNode.Flags.Bend) != 0 && count == 2)
            {
                float angle = BendAngle(node, segments, id);
                Offer(best, "bend-node", true, id, angle, "bends " + Degrees(angle));
            }
            else if ((node.m_flags & NetNode.Flags.End) != 0 && count == 1)
            {
                Offer(best, "dead-end", true, id, -i, "1 segment");
            }
        }

        var targets = new List<ProbeTarget>();
        foreach (string label in new[] { "straight-segment", "curved-segment", "sloped-segment", "turned-segment", "junction-4way", "junction-t", "junction-mixed-widths", "junction-rendered", "bend-node", "dead-end" })
        {
            if (best.TryGetValue(label, out ProbeTarget? target))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private static void Offer(Dictionary<string, ProbeTarget> best, string label, bool isNode, ushort id, float score, string reason)
    {
        if (!best.TryGetValue(label, out ProbeTarget? current) || score > current.Score)
        {
            best[label] = new ProbeTarget(label, isNode, id, score, reason);
        }
    }

    private static bool HasTurnedMesh(NetSegment segment)
    {
        foreach (NetInfo.Segment mesh in segment.Info.m_segments)
        {
            if (mesh.CheckFlags(segment.m_flags, segment.m_flags2, out bool turnAround) && turnAround)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRoad(NetInfo? info) =>
        info is not null && info.m_netAI is RoadAI && info.m_class is not null && info.m_class.m_service == ItemClass.Service.Road;

    private static bool IsUsableSegment(NetSegment segment) =>
        (segment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted | NetSegment.Flags.Untouchable)) == NetSegment.Flags.Created
        && IsRoad(segment.Info)
        && segment.Info.m_segments is { Length: > 0 };

    private static bool IsUsableNode(NetNode node, NetSegment[] segments, out int count, out bool mixedWidths)
    {
        count = 0;
        mixedWidths = false;
        const NetNode.Flags Excluded = NetNode.Flags.Deleted | NetNode.Flags.Untouchable | NetNode.Flags.Outside;
        if ((node.m_flags & NetNode.Flags.Created) == 0 || (node.m_flags & Excluded) != 0 || !IsRoad(node.Info))
        {
            return false;
        }

        float firstWidth = -1f;
        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            NetSegment segment = segments[segmentId];
            if (!IsUsableSegment(segment) || segment.Info.m_requireDirectRenderers)
            {
                return false;
            }

            count++;
            float width = segment.Info.m_halfWidth;
            if (firstWidth < 0f)
            {
                firstWidth = width;
            }
            else if (Mathf.Abs(width - firstWidth) > 0.5f)
            {
                mixedWidths = true;
            }
        }

        return count > 0;
    }

    private static float BendAngle(NetNode node, NetSegment[] segments, ushort nodeId)
    {
        var directions = new List<Vector2>();
        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId != 0)
            {
                NetSegment segment = segments[segmentId];
                Vector3 direction = segment.m_startNode == nodeId ? segment.m_startDirection : segment.m_endDirection;
                directions.Add(new Vector2(direction.x, direction.z).normalized);
            }
        }

        return directions.Count == 2 ? Mathf.Acos(Mathf.Clamp(-Vector2.Dot(directions[0], directions[1]), -1f, 1f)) : 0f;
    }

    private static ProbeResult Probe(ProbeTarget target, ProbeMeshRegistry registry)
    {
        NetManager net = Singleton<NetManager>.instance;
        var result = new ProbeResult(target);
        if (target.IsNode)
        {
            NetNode node = net.m_nodes.m_buffer[target.Id];
            result.GroupPosition = node.m_position;
            result.Renderer = NetGroupPieces.ReadRendererNodeData(target.Id);
            foreach (int layer in NodeLayers(node, net.m_segments.m_buffer))
            {
                var probeLayer = new ProbeLayer(layer);
                probeLayer.Pieces.AddRange(NetGroupPieces.ForNode(target.Id, layer, result.GroupPosition, result.Details));
                int vertexCount = 0;
                int triangleCount = 0;
                int objectCount = 0;
                RenderGroup.VertexArrays arrays = 0;
                _ = node.CalculateGroupData(target.Id, layer, ref vertexCount, ref triangleCount, ref objectCount, ref arrays);
                if (vertexCount > 0)
                {
                    var data = new RenderGroup.MeshData(NetGroupPieces.AllArrays, vertexCount, triangleCount);
                    int vertexIndex = 0;
                    int triangleIndex = 0;
                    Vector3 min = new(100000f, 100000f, 100000f);
                    Vector3 max = new(-100000f, -100000f, -100000f);
                    float maxRenderDistance = 0f;
                    float maxInstanceDistance = 0f;
                    bool requireSurfaceMaps = false;
                    node.PopulateGroupData(target.Id, 0, 0, layer, ref vertexIndex, ref triangleIndex, result.GroupPosition, data, ref min, ref max, ref maxRenderDistance, ref maxInstanceDistance, ref requireSurfaceMaps);
                    probeLayer.SetGameOutput(data, vertexCount, triangleCount, vertexIndex, triangleIndex);
                }

                result.Layers.Add(probeLayer);
            }
        }
        else
        {
            NetSegment segment = net.m_segments.m_buffer[target.Id];
            NetNode[] nodes = net.m_nodes.m_buffer;
            result.GroupPosition = (nodes[segment.m_startNode].m_position + nodes[segment.m_endNode].m_position) * 0.5f;
            foreach (int layer in SegmentLayers(segment.Info))
            {
                var probeLayer = new ProbeLayer(layer);
                probeLayer.Pieces.AddRange(NetGroupPieces.ForSegment(target.Id, layer, result.GroupPosition));
                int vertexCount = 0;
                int triangleCount = 0;
                int objectCount = 0;
                RenderGroup.VertexArrays arrays = 0;
                _ = segment.CalculateGroupData(target.Id, layer, ref vertexCount, ref triangleCount, ref objectCount, ref arrays);
                if (vertexCount > 0)
                {
                    var data = new RenderGroup.MeshData(NetGroupPieces.AllArrays, vertexCount, triangleCount);
                    int vertexIndex = 0;
                    int triangleIndex = 0;
                    Vector3 min = new(100000f, 100000f, 100000f);
                    Vector3 max = new(-100000f, -100000f, -100000f);
                    float maxRenderDistance = 0f;
                    float maxInstanceDistance = 0f;
                    bool requireSurfaceMaps = false;
                    segment.PopulateGroupData(target.Id, 0, 0, layer, ref vertexIndex, ref triangleIndex, result.GroupPosition, data, ref min, ref max, ref maxRenderDistance, ref maxInstanceDistance, ref requireSurfaceMaps);
                    probeLayer.SetGameOutput(data, vertexCount, triangleCount, vertexIndex, triangleIndex);
                }

                result.Layers.Add(probeLayer);
            }
        }

        foreach (ProbeLayer layer in result.Layers)
        {
            foreach (GroupPiece piece in layer.Pieces)
            {
                RegisterMeshes(piece, registry);
            }
        }

        return result;
    }

    private static List<int> SegmentLayers(NetInfo info)
    {
        var layers = new List<int>();
        AddLayers(layers, info.m_segments, null);
        return layers;
    }

    private static List<int> NodeLayers(NetNode node, NetSegment[] segments)
    {
        var layers = new List<int>();
        NetInfo info = node.Info;
        AddLayers(layers, info.m_segments, info.m_nodes);
        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId != 0)
            {
                AddLayers(layers, null, segments[segmentId].Info.m_nodes);
            }
        }

        layers.Sort();
        return layers;
    }

    private static void AddLayers(List<int> layers, NetInfo.Segment[]? segmentMeshes, NetInfo.Node[]? nodeMeshes)
    {
        if (segmentMeshes is not null)
        {
            foreach (NetInfo.Segment mesh in segmentMeshes)
            {
                if (!layers.Contains(mesh.m_layer))
                {
                    layers.Add(mesh.m_layer);
                }
            }
        }

        if (nodeMeshes is not null)
        {
            foreach (NetInfo.Node mesh in nodeMeshes)
            {
                if (!layers.Contains(mesh.m_layer))
                {
                    layers.Add(mesh.m_layer);
                }
            }
        }
    }

    private static void RegisterMeshes(GroupPiece piece, ProbeMeshRegistry registry)
    {
        piece.SourceDataId = registry.Register(piece.SourceData, "lodData");
        if (piece.SegmentMesh is not null)
        {
            piece.MainMeshId = registry.Register(piece.SegmentMesh.m_segmentMesh, "main");
            piece.SourceMeshId = registry.Register(piece.SegmentMesh.m_mesh, "source");
            piece.LodMeshId = registry.Register(piece.SegmentMesh.m_lodMesh, "lod");
        }
        else if (piece.NodeMesh is not null)
        {
            piece.MainMeshId = registry.Register(piece.NodeMesh.m_nodeMesh, "main");
            piece.SourceMeshId = registry.Register(piece.NodeMesh.m_mesh, "source");
            piece.LodMeshId = registry.Register(piece.NodeMesh.m_lodMesh, "lod");
        }
    }

    private static void WriteFile(JsonWriter json, List<ProbeResult> results, ProbeMeshRegistry registry)
    {
        json.WriteStartObject();
        json.WriteString("format", FormatName);
        json.WriteNumber("version", FormatVersion);
        json.WriteString("game", BuildConfig.applicationVersion);
        json.WriteString(
            "notes",
            "Developer probe (plan step 0.7). Matrices are written row by row (m00 m01 m02 m03 m10 ...). Coordinates are the game's, relative to each object's groupPosition. Pieces list the inputs of the game's static PopulateGroupData functions and their output; layers[].game is the output of the instance PopulateGroupData for the whole object.");
        json.WriteStartArray("objects");
        foreach (ProbeResult result in results)
        {
            WriteResult(json, result);
        }

        json.WriteEndArray();
        json.WriteStartArray("meshes");
        for (int i = 0; i < registry.Meshes.Count; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("id", i);
            json.WriteString("role", registry.Roles[i]);
            object mesh = registry.Meshes[i];
            if (mesh is RenderGroup.MeshData data)
            {
                json.WriteString("type", "meshData");
                ProbeJson.WriteMeshData(json, data, data.m_vertices.Length, data.m_triangles.Length);
            }
            else if (mesh is Mesh unityMesh)
            {
                json.WriteString("type", "unityMesh");
                ProbeJson.WriteUnityMesh(json, unityMesh);
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteResult(JsonWriter json, ProbeResult result)
    {
        ProbeTarget target = result.Target;
        NetManager net = Singleton<NetManager>.instance;
        json.WriteStartObject();
        json.WriteString("label", target.Label);
        json.WriteString("kind", target.IsNode ? "node" : "segment");
        json.WriteNumber("id", target.Id);
        json.WriteString("reason", target.Reason);
        if (result.Error is not null)
        {
            json.WriteString("error", result.Error);
        }

        ProbeJson.WriteVector(json, "groupPosition", result.GroupPosition);
        if (target.IsNode)
        {
            WriteNode(json, net, target.Id, result.Details);
            json.WriteStartArray("renderer");
            foreach (GpuParameters instance in result.Renderer)
            {
                json.WriteStartObject();
                WriteGpu(json, instance);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }
        else
        {
            WriteSegment(json, net, target.Id);
        }

        json.WriteStartArray("layers");
        foreach (ProbeLayer layer in result.Layers)
        {
            json.WriteStartObject();
            json.WriteNumber("layer", layer.Layer);
            if (layer.GameOutput is not null)
            {
                json.WriteStartObject("game");
                json.WriteNumber("expectedVertexCount", layer.ExpectedVertexCount);
                json.WriteNumber("expectedTriangleIndexCount", layer.ExpectedTriangleCount);
                ProbeJson.WriteMeshData(json, layer.GameOutput, layer.WrittenVertexCount, layer.WrittenTriangleCount);
                json.WriteEndObject();
            }

            json.WriteStartArray("pieces");
            foreach (GroupPiece piece in layer.Pieces)
            {
                WritePiece(json, piece);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteSegment(JsonWriter json, NetManager net, ushort id)
    {
        NetSegment segment = net.m_segments.m_buffer[id];
        NetNode[] nodes = net.m_nodes.m_buffer;
        NetInfo info = segment.Info;
        json.WriteString("info", info.name);
        json.WriteString("flags", segment.m_flags.ToString());
        json.WriteNumber("startNode", segment.m_startNode);
        json.WriteNumber("endNode", segment.m_endNode);
        ProbeJson.WriteVector(json, "startPosition", nodes[segment.m_startNode].m_position);
        ProbeJson.WriteVector(json, "endPosition", nodes[segment.m_endNode].m_position);
        ProbeJson.WriteVector(json, "startDirection", segment.m_startDirection);
        ProbeJson.WriteVector(json, "endDirection", segment.m_endDirection);
        json.WriteNumber("averageLength", segment.m_averageLength);
        WriteInfo(json, info);
    }

    private static void WriteNode(JsonWriter json, NetManager net, ushort id, NodeDetails details)
    {
        NetNode node = net.m_nodes.m_buffer[id];
        json.WriteString("info", node.Info.name);
        json.WriteString("flags", node.m_flags.ToString());
        ProbeJson.WriteVector(json, "position", node.m_position);
        json.WriteNumber("heightOffset", node.m_heightOffset);
        json.WriteNumber("connectCount", node.m_connectCount);
        WriteInfo(json, node.Info);
        json.WriteStartArray("segments");
        for (int i = 0; i < 8; i++)
        {
            ushort segmentId = node.GetSegment(i);
            if (segmentId == 0)
            {
                continue;
            }

            NetSegment segment = net.m_segments.m_buffer[segmentId];
            json.WriteStartObject();
            json.WriteNumber("slot", i);
            json.WriteNumber("id", segmentId);
            json.WriteString("info", segment.Info.name);
            json.WriteBoolean("startsHere", segment.m_startNode == id);
            ProbeJson.WriteVector(json, "direction", segment.m_startNode == id ? segment.m_startDirection : segment.m_endDirection);
            json.WriteNumber("halfWidth", segment.Info.m_halfWidth);
            json.WriteNumber("pavementWidth", segment.Info.m_pavementWidth);
            if (details.DeltaDirections is not null)
            {
                ProbeJson.WriteVector(json, "deltaDirection", details.DeltaDirections[i]);
            }

            if (details.CenterPositions is not null)
            {
                ProbeJson.WriteVector(json, "centerPosition", details.CenterPositions[i]);
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void WriteInfo(JsonWriter json, NetInfo info)
    {
        json.WriteStartObject("netInfo");
        json.WriteString("name", info.name);
        json.WriteNumber("halfWidth", info.m_halfWidth);
        json.WriteNumber("pavementWidth", info.m_pavementWidth);
        json.WriteNumber("segmentLength", info.m_segmentLength);
        json.WriteNumber("minCornerOffset", info.m_minCornerOffset);
        json.WriteNumber("vScale", info.m_netAI.GetVScale());
        json.WriteNumber("endRadius", info.m_netAI.GetEndRadius());
        json.WriteEndObject();
    }

    private static void WritePiece(JsonWriter json, GroupPiece piece)
    {
        json.WriteStartObject();
        json.WriteString("kind", piece.Kind switch
        {
            GroupPieceKind.Segment => "segment",
            GroupPieceKind.Junction => "junction",
            GroupPieceKind.End => "end",
            GroupPieceKind.BendSegment => "bendSegment",
            _ => "bendNode",
        });
        json.WriteString("info", piece.Info.name);
        json.WriteNumber("meshIndex", piece.MeshIndex);
        json.WriteNumber("segment", piece.SegmentId);
        json.WriteBoolean("turnAround", piece.TurnAround);
        json.WriteBoolean("requireHeightMap", piece.RequiresHeightMap);
        json.WriteNumber("sourceData", piece.SourceDataId);
        json.WriteNumber("mainMesh", piece.MainMeshId);
        json.WriteNumber("sourceMesh", piece.SourceMeshId);
        json.WriteNumber("lodMesh", piece.LodMeshId);
        ProbeJson.WriteMatrix(json, "left", piece.Left);
        ProbeJson.WriteMatrix(json, "right", piece.Right);
        if (piece.Kind is GroupPieceKind.Junction or GroupPieceKind.End)
        {
            ProbeJson.WriteMatrix(json, "leftB", piece.LeftB);
            ProbeJson.WriteMatrix(json, "rightB", piece.RightB);
            ProbeJson.WriteVector(json, "centerPos", piece.CenterPos);
            ProbeJson.WriteVector(json, "sideScale", piece.SideScale);
        }

        ProbeJson.WriteVector(json, "meshScale", piece.MeshScale);
        ProbeJson.WriteVector(json, "objectIndex", piece.ObjectIndex);
        ProbeJson.WriteVector(json, "colorIndex", piece.ColorIndex);
        if (piece.Output is not null)
        {
            json.WriteStartObject("output");
            ProbeJson.WriteMeshData(json, piece.Output, piece.OutputVertexCount, piece.OutputTriangleCount);
            json.WriteEndObject();
        }

        if (piece.MainOutput is not null)
        {
            json.WriteStartObject("mainOutput");
            ProbeJson.WriteMeshData(json, piece.MainOutput, piece.MainOutputVertexCount, piece.MainOutputTriangleCount);
            json.WriteEndObject();
        }
        else if (piece.MainOutputNote is not null)
        {
            json.WriteString("mainOutputNote", piece.MainOutputNote);
        }

        if (piece.Gpu is not null)
        {
            json.WriteNumber("slot", piece.Slot);
            json.WriteStartObject("gpu");
            WriteGpu(json, piece.Gpu);
            if (piece.Gpu.MainOutput is not null)
            {
                json.WriteStartObject("mainOutput");
                ProbeJson.WriteMeshData(json, piece.Gpu.MainOutput, piece.Gpu.MainOutputVertexCount, piece.Gpu.MainOutputTriangleCount);
                json.WriteEndObject();
            }

            json.WriteEndObject();
        }

        json.WriteEndObject();
    }

    private static void WriteGpu(JsonWriter json, GpuParameters gpu)
    {
        ProbeJson.WriteVector(json, "position", gpu.Position);
        ProbeJson.WriteMatrix(json, "left", gpu.Left);
        ProbeJson.WriteMatrix(json, "right", gpu.Right);
        ProbeJson.WriteMatrix(json, "leftB", gpu.LeftB);
        ProbeJson.WriteMatrix(json, "rightB", gpu.RightB);
        ProbeJson.WriteVector(json, "meshScale", gpu.MeshScale);
        ProbeJson.WriteVector(json, "centerPos", gpu.CenterPos);
        ProbeJson.WriteVector(json, "sideScale", gpu.SideScale);
    }

    private static string Metres(float value) => value.ToString("0.0", CultureInfo.InvariantCulture) + " m";

    private static string Degrees(float radians) => (radians * Mathf.Rad2Deg).ToString("0.0", CultureInfo.InvariantCulture) + "°";

    private sealed class ProbeTarget
    {
        public ProbeTarget(string label, bool isNode, ushort id, float score, string reason)
        {
            Label = label;
            IsNode = isNode;
            Id = id;
            Score = score;
            Reason = reason;
        }

        public string Label { get; }

        public bool IsNode { get; }

        public ushort Id { get; }

        public float Score { get; }

        public string Reason { get; }
    }

    private sealed class ProbeResult
    {
        public ProbeResult(ProbeTarget target) => Target = target;

        public ProbeTarget Target { get; }

        public Vector3 GroupPosition { get; set; }

        public NodeDetails Details { get; } = new();

        /// <summary>The renderer's own instance data for a node, if the game drew it recently.</summary>
        public List<GpuParameters> Renderer { get; set; } = new();

        public List<ProbeLayer> Layers { get; } = new();

        public string? Error { get; set; }
    }

    private sealed class ProbeLayer
    {
        public ProbeLayer(int layer) => Layer = layer;

        public int Layer { get; }

        public List<GroupPiece> Pieces { get; } = new();

        public RenderGroup.MeshData? GameOutput { get; private set; }

        public int ExpectedVertexCount { get; private set; }

        public int ExpectedTriangleCount { get; private set; }

        public int WrittenVertexCount { get; private set; }

        public int WrittenTriangleCount { get; private set; }

        public void SetGameOutput(RenderGroup.MeshData data, int expectedVertices, int expectedTriangles, int writtenVertices, int writtenTriangles)
        {
            GameOutput = data;
            ExpectedVertexCount = expectedVertices;
            ExpectedTriangleCount = expectedTriangles;
            WrittenVertexCount = writtenVertices;
            WrittenTriangleCount = writtenTriangles;
        }
    }
}

/// <summary>What the probe learns about a junction while computing its pieces.</summary>
internal sealed class NodeDetails
{
    /// <summary>Per segment slot, how far its share of the centre is pushed (the game's delta directions).</summary>
    public Vector3[]? DeltaDirections { get; set; }

    /// <summary>Per segment slot, the centre position its junction piece blends to.</summary>
    public Vector3[]? CenterPositions { get; set; }
}
