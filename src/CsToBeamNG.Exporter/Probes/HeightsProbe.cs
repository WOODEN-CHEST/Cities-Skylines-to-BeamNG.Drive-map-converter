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
/// The heights probe, a developer tool for research (plan step 1.2): it evaluates the game's own terrain height
/// samplers at road nodes, the middles of road segments, buildings and their lot corners, and at chosen test
/// points, and counts where the game's height arrays differ. The converter compares this with the dumped terrain
/// (<c>cstobeamng dev height-probe</c>; docs/research/cs1-save-and-world.md, Terrain). It runs on the simulation
/// thread, inside an export, and only reads.
/// </summary>
internal static class HeightsProbe
{
    /// <summary>The probe file's format name and version, checked by the converter's reader.</summary>
    public const string FormatName = "cstobeamng-heights-probe";

    public const int FormatVersion = 1;

    private const int NodeCount = 60;
    private const int SegmentCount = 40;
    private const int BuildingCount = 60;
    private const int RandomPointCount = 300;
    private const int EdgePointCount = 100;
    private const float MetersPerUnit = 1f / 64;
    private const float LotCellMeters = 8;
    private const float LotCornerInsetMeters = 2;

    /// <summary>Writes the probe into a folder and returns a one-line summary.</summary>
    /// <param name="folder">The probe's folder; created if needed.</param>
    /// <param name="log">The export's log.</param>
    public static string Write(string folder, ExportLog log)
    {
        if (!Singleton<TerrainManager>.exists)
        {
            return "skipped: there is no terrain";
        }

        TerrainManager terrain = Singleton<TerrainManager>.instance;
        var samples = new List<Sample>();
        if (Singleton<NetManager>.exists)
        {
            AddNodes(Singleton<NetManager>.instance, samples);
            AddSegments(Singleton<NetManager>.instance, samples);
        }

        if (Singleton<BuildingManager>.exists)
        {
            AddBuildings(Singleton<BuildingManager>.instance, samples);
        }

        AddPoints(terrain, samples);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, DumpProbes.HeightsFileName);
        using (JsonWriter json = JsonWriter.CreateFile(path))
        {
            json.WriteStartObject();
            json.WriteString("format", FormatName);
            json.WriteNumber("version", FormatVersion);
            json.WriteString("game", BuildConfig.applicationVersion);
            json.WriteString(
                "notes",
                "Developer probe (plan step 1.2). Heights in metres from the game's TerrainManager samplers at each sample's x and z: detail = SampleDetailHeight, detailSmooth = SampleDetailHeightSmooth, final = SampleFinalHeight, finalSmooth = SampleFinalHeightSmooth, raw = SampleRawHeight (m_rawHeights2), rawSmooth = SampleRawHeightSmooth (m_rawHeights2), originalRawSmooth = SampleOriginalRawHeightSmooth (m_rawHeights), blockSmooth = SampleBlockHeightSmooth, surface = SampleRawHeightSmoothWithWater(timeLerp false, offset 0). y is the object's own height.");
            WriteArrayComparison(json, terrain);
            json.WriteStartArray("samples");
            foreach (Sample sample in samples)
            {
                WriteSample(json, terrain, sample);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        log.Detail("Heights probe: " + samples.Count.ToString(CultureInfo.InvariantCulture) + " samples");
        return string.Format(CultureInfo.InvariantCulture, "{0} samples, written to {1}", samples.Count, path);
    }

    private static void AddNodes(NetManager net, List<Sample> samples)
    {
        NetNode[] nodes = net.m_nodes.m_buffer;
        var onGround = new List<int>();
        var other = new List<int>();
        for (int i = 1; i < nodes.Length; i++)
        {
            NetNode.Flags flags = nodes[i].m_flags;
            if ((flags & NetNode.Flags.Created) == 0 || (flags & NetNode.Flags.Deleted) != 0 || nodes[i].Info is null)
            {
                continue;
            }

            ((flags & NetNode.Flags.OnGround) != 0 ? onGround : other).Add(i);
        }

        foreach (int id in Spread(onGround, NodeCount * 3 / 4))
        {
            AddNode(nodes, id, samples);
        }

        foreach (int id in Spread(other, NodeCount / 4))
        {
            AddNode(nodes, id, samples);
        }
    }

    private static void AddNode(NetNode[] nodes, int id, List<Sample> samples)
    {
        NetNode node = nodes[id];
        samples.Add(new Sample("node", id, node.Info.name, ((uint)node.m_flags).ToString("X8", CultureInfo.InvariantCulture), node.m_position)
        {
            Extra = "elevation " + node.m_elevation.ToString(CultureInfo.InvariantCulture),
        });
    }

    private static void AddSegments(NetManager net, List<Sample> samples)
    {
        NetSegment[] segments = net.m_segments.m_buffer;
        NetNode[] nodes = net.m_nodes.m_buffer;
        var ids = new List<int>();
        for (int i = 1; i < segments.Length; i++)
        {
            NetSegment segment = segments[i];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || (segment.m_flags & NetSegment.Flags.Deleted) != 0 || segment.Info is null)
            {
                continue;
            }

            // Segments between two nodes on the ground, so the road lies on the terrain.
            if ((nodes[segment.m_startNode].m_flags & nodes[segment.m_endNode].m_flags & NetNode.Flags.OnGround) != 0)
            {
                ids.Add(i);
            }
        }

        foreach (int id in Spread(ids, SegmentCount))
        {
            NetSegment segment = segments[id];
            samples.Add(new Sample("segment", id, segment.Info.name, ((uint)segment.m_flags).ToString("X8", CultureInfo.InvariantCulture), segment.m_middlePosition));
        }
    }

    private static void AddBuildings(BuildingManager manager, List<Sample> samples)
    {
        Building[] buildings = manager.m_buildings.m_buffer;
        var ids = new List<int>();
        for (int i = 1; i < buildings.Length; i++)
        {
            Building.Flags flags = buildings[i].m_flags;
            if ((flags & Building.Flags.Created) != 0 && (flags & Building.Flags.Deleted) == 0 && buildings[i].Info is not null)
            {
                ids.Add(i);
            }
        }

        foreach (int id in Spread(ids, BuildingCount))
        {
            Building building = buildings[id];
            string flags = ((uint)building.m_flags).ToString("X8", CultureInfo.InvariantCulture);
            string extra = string.Format(
                CultureInfo.InvariantCulture,
                "lot {0} x {1} cells, base height {2}, parent {3}",
                building.m_width,
                building.m_length,
                building.m_baseHeight,
                building.m_parentBuilding);
            samples.Add(new Sample("building", id, building.Info.name, flags, building.m_position) { Extra = extra });

            // The lot's corners, a little inside it: the building's local X is its width, Z its length.
            float halfWidth = Mathf.Max((building.m_width * LotCellMeters / 2) - LotCornerInsetMeters, 0);
            float halfLength = Mathf.Max((building.m_length * LotCellMeters / 2) - LotCornerInsetMeters, 0);
            for (int corner = 0; corner < 4; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -halfWidth : halfWidth, 0, (corner & 2) == 0 ? -halfLength : halfLength);
                samples.Add(new Sample("lot-corner", id, building.Info.name, flags, building.CalculatePosition(offset)) { Extra = extra });
            }
        }
    }

    // Test points for comparing the converter's sampling with the game's: random points around the tiles that have
    // detail heights, points on those tiles' edges, and random points anywhere on the map.
    private static void AddPoints(TerrainManager terrain, List<Sample> samples)
    {
        const float TileSize = TerrainManager.DETAIL_RESOLUTION * TerrainManager.DETAIL_CELL_SIZE;
        const float HalfMap = TileSize * TerrainManager.PATCH_RESOLUTION / 2;
        int minX = int.MaxValue;
        int minZ = int.MaxValue;
        int maxX = int.MinValue;
        int maxZ = int.MinValue;
        var detailed = new List<int>();
        for (int i = 0; i < terrain.m_patches.Length; i++)
        {
            if (terrain.m_patches[i].m_simDetailIndex > 0)
            {
                detailed.Add(i);
                int x = i % TerrainManager.PATCH_RESOLUTION;
                int z = i / TerrainManager.PATCH_RESOLUTION;
                minX = Math.Min(minX, x);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxZ = Math.Max(maxZ, z);
            }
        }

        var random = new Lcg(20261117);
        int id = 0;
        if (detailed.Count > 0)
        {
            // One tile beyond the detailed ones on each side, within the map.
            float left = -HalfMap + (Math.Max(minX - 1, 0) * TileSize);
            float right = -HalfMap + (Math.Min(maxX + 2, TerrainManager.PATCH_RESOLUTION) * TileSize);
            float bottom = -HalfMap + (Math.Max(minZ - 1, 0) * TileSize);
            float top = -HalfMap + (Math.Min(maxZ + 2, TerrainManager.PATCH_RESOLUTION) * TileSize);
            for (int i = 0; i < RandomPointCount; i++)
            {
                AddPoint(samples, id++, "near detail", random.Between(left, right), random.Between(bottom, top));
            }

            for (int i = 0; i < EdgePointCount; i++)
            {
                int patch = detailed[random.Below(detailed.Count)];
                float x0 = -HalfMap + ((patch % TerrainManager.PATCH_RESOLUTION) * TileSize);
                float z0 = -HalfMap + ((patch / TerrainManager.PATCH_RESOLUTION) * TileSize);
                float along = random.Between(0, TileSize);
                float edge = (i & 2) == 0 ? 0 : TileSize;
                AddPoint(samples, id++, "tile edge", (i & 1) == 0 ? x0 + edge : x0 + along, (i & 1) == 0 ? z0 + along : z0 + edge);
            }
        }

        for (int i = 0; i < RandomPointCount / 3; i++)
        {
            AddPoint(samples, id++, "anywhere", random.Between(-HalfMap, HalfMap), random.Between(-HalfMap, HalfMap));
        }
    }

    private static void AddPoint(List<Sample> samples, int id, string label, float x, float z) =>
        samples.Add(new Sample("point", id, label, "0", new Vector3(x, 0, z)));

    // Up to count ids spread evenly over the list, keeping its order.
    private static IEnumerable<int> Spread(List<int> ids, int count)
    {
        if (ids.Count <= count)
        {
            foreach (int id in ids)
            {
                yield return id;
            }

            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            yield return ids[(int)((long)i * ids.Count / count)];
        }
    }

    private static void WriteSample(JsonWriter json, TerrainManager terrain, Sample sample)
    {
        Vector3 position = sample.Position;
        float gridX = (position.x / TerrainManager.RAW_CELL_SIZE) + (TerrainManager.RAW_RESOLUTION / 2);
        float gridZ = (position.z / TerrainManager.RAW_CELL_SIZE) + (TerrainManager.RAW_RESOLUTION / 2);
        json.WriteStartObject();
        json.WriteString("kind", sample.Kind);
        json.WriteNumber("id", sample.Id);
        json.WriteString("label", sample.Label);
        json.WriteString("flags", sample.Flags);
        if (sample.Extra is not null)
        {
            json.WriteString("extra", sample.Extra);
        }

        json.WriteNumber("x", position.x);
        json.WriteNumber("y", position.y);
        json.WriteNumber("z", position.z);
        json.WriteNumber("detail", terrain.SampleDetailHeight(position));
        json.WriteNumber("detailSmooth", terrain.SampleDetailHeightSmooth(position));
        json.WriteNumber("final", terrain.SampleFinalHeight(gridX, gridZ) * MetersPerUnit);
        json.WriteNumber("finalSmooth", terrain.SampleFinalHeightSmooth(gridX, gridZ) * MetersPerUnit);
        json.WriteNumber("raw", terrain.SampleRawHeight(gridX, gridZ) * MetersPerUnit);
        json.WriteNumber("rawSmooth", terrain.SampleRawHeightSmooth(position));
        json.WriteNumber("originalRawSmooth", terrain.SampleOriginalRawHeightSmooth(position));
        json.WriteNumber("blockSmooth", terrain.SampleBlockHeightSmooth(position));
        json.WriteNumber("surface", terrain.SampleRawHeightSmoothWithWater(position, false, 0));
        json.WriteEndObject();
    }

    // Where the game's height arrays differ, to show which modifications each one contains.
    private static void WriteArrayComparison(JsonWriter json, TerrainManager terrain)
    {
        json.WriteStartObject("arrays");
        Compare(json, "rawVsRaw2", terrain.RawHeights, terrain.RawHeights2);
        Compare(json, "finalVsRaw", terrain.FinalHeights, terrain.RawHeights);
        Compare(json, "finalVsBlockTargets", terrain.FinalHeights, terrain.BlockHeightTargets);
        Compare(json, "blockVsBlockTargets", terrain.BlockHeights, terrain.BlockHeightTargets);
        Compare(json, "detailVsDetail2", terrain.m_detailHeights, terrain.m_detailHeights2);
        json.WriteNumber("detailPatchCount", terrain.m_detailPatchCount);
        json.WriteEndObject();
    }

    private static void Compare(JsonWriter json, string name, ushort[]? a, ushort[]? b)
    {
        json.WriteStartObject(name);
        if (a is null || b is null || a.Length != b.Length)
        {
            json.WriteString("problem", "missing or of different lengths");
            json.WriteEndObject();
            return;
        }

        int differing = 0;
        int maxAbove = 0;
        int maxBelow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int difference = a[i] - b[i];
            if (difference != 0)
            {
                differing++;
                maxAbove = Math.Max(maxAbove, difference);
                maxBelow = Math.Min(maxBelow, difference);
            }
        }

        json.WriteNumber("values", a.Length);
        json.WriteNumber("differing", differing);
        json.WriteNumber("maxAbove", maxAbove * MetersPerUnit);
        json.WriteNumber("maxBelow", maxBelow * MetersPerUnit);
        json.WriteEndObject();
    }

    private sealed class Sample
    {
        public Sample(string kind, int id, string label, string flags, Vector3 position)
        {
            Kind = kind;
            Id = id;
            Label = label;
            Flags = flags;
            Position = position;
        }

        public string Kind { get; }

        public int Id { get; }

        public string Label { get; }

        public string Flags { get; }

        public Vector3 Position { get; }

        public string? Extra { get; set; }
    }

    // A small deterministic random generator (Numerical Recipes' LCG), so every run probes the same points.
    private sealed class Lcg
    {
        private uint _state;

        public Lcg(uint seed) => _state = seed;

        public float Between(float min, float max) => min + ((max - min) * (Next() >> 8) / (1 << 24));

        public int Below(int count) => (int)((ulong)Next() * (ulong)count >> 32);

        private uint Next()
        {
            _state = unchecked((_state * 1664525) + 1013904223);
            return _state;
        }
    }
}
