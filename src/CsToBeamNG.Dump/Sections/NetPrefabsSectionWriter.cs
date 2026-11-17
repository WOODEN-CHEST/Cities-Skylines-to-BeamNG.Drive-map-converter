using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>net-prefabs</c> section (docs/dump-format.md, <c>net-prefabs</c>).</summary>
public static class NetPrefabsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Prefabs are written ordered by
    /// index, whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The prefabs.</param>
    /// <exception cref="ArgumentException">A prefab is missing, listed twice or can't be written (<see cref="Check"/>).</exception>
    public static void Write(DumpWriter dump, NetPrefabsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        var prefabs = new List<NetPrefabRecord>(content.Prefabs);
        foreach (NetPrefabRecord prefab in prefabs)
        {
            Guard.NotNull(prefab, nameof(content));
            string? problem = Check(prefab);
            if (problem is not null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Prefab {0} can't be written: {1}.", prefab.Index, problem),
                    nameof(content));
            }
        }

        prefabs.Sort((a, b) => a.Index.CompareTo(b.Index));
        for (int i = 1; i < prefabs.Count; i++)
        {
            if (prefabs[i].Index == prefabs[i - 1].Index)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Prefab {0} is listed twice.", prefabs[i].Index),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(NetPrefabsSection.Name, NetPrefabsSection.Version);
        using (JsonWriter json = section.CreateJsonFile(NetPrefabsSection.PrefabsFileName))
        {
            json.WriteStartObject();
            json.WriteStartArray("prefabs");
            foreach (NetPrefabRecord prefab in prefabs)
            {
                WritePrefab(json, prefab);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        section.RecordCount = prefabs.Count;
        section.Complete();
    }

    /// <summary>
    /// Returns why a prefab can't be written, such as a missing name or a width that isn't a finite number, or
    /// <see langword="null"/> if it can. The exporter uses it to skip such a prefab instead of failing the section.
    /// </summary>
    /// <param name="prefab">The prefab.</param>
    public static string? Check(NetPrefabRecord prefab)
    {
        Guard.NotNull(prefab, nameof(prefab));
        if (prefab.Name is null || prefab.ClassName is null || prefab.Service is null || prefab.SubService is null
            || prefab.Level is null || prefab.AiType is null)
        {
            return "its name, its class's name, service, sub-service or level, or its AI's type is missing";
        }

        if (!IsFinite(prefab.HalfWidth) || !IsFinite(prefab.PavementWidth) || !IsFinite(prefab.SegmentLength) || !IsFinite(prefab.SurfaceLevel))
        {
            return "its half width, pavement width, segment length or surface level isn't a finite number";
        }

        for (int i = 0; i < prefab.Lanes.Count; i++)
        {
            NetPrefabLane lane = prefab.Lanes[i];
            if (lane is null)
            {
                return string.Format(CultureInfo.InvariantCulture, "lane {0} is missing", i);
            }

            if (!IsFinite(lane.Position) || !IsFinite(lane.Width) || !IsFinite(lane.VerticalOffset) || !IsFinite(lane.StopOffset) || !IsFinite(lane.SpeedLimit))
            {
                return string.Format(CultureInfo.InvariantCulture, "a value of lane {0} isn't a finite number", i);
            }
        }

        return null;
    }

    private static void WritePrefab(JsonWriter json, NetPrefabRecord prefab)
    {
        json.WriteStartObject();
        json.WriteNumber("index", prefab.Index);
        json.WriteString("name", prefab.Name);
        json.WriteStartObject("class");
        json.WriteString("name", prefab.ClassName);
        json.WriteString("service", prefab.Service);
        json.WriteString("subService", prefab.SubService);
        json.WriteString("level", prefab.Level);
        json.WriteNumber("layer", prefab.Layer);
        json.WriteEndObject();
        json.WriteStartObject("ai");
        json.WriteString("type", prefab.AiType);
        json.WriteBoolean("highway", prefab.Highway);
        json.WriteBoolean("bridge", prefab.Bridge);
        json.WriteBoolean("tunnel", prefab.Tunnel);
        json.WriteBoolean("underground", prefab.Underground);
        json.WriteBoolean("overground", prefab.Overground);
        json.WriteEndObject();
        json.WriteNumber("halfWidth", prefab.HalfWidth);
        json.WriteNumber("pavementWidth", prefab.PavementWidth);
        json.WriteNumber("segmentLength", prefab.SegmentLength);
        json.WriteNumber("surfaceLevel", prefab.SurfaceLevel);
        json.WriteStartObject("terrain");
        json.WriteBoolean("createPavement", prefab.CreatePavement);
        json.WriteBoolean("createGravel", prefab.CreateGravel);
        json.WriteBoolean("createRuining", prefab.CreateRuining);
        json.WriteBoolean("flattenTerrain", prefab.FlattenTerrain);
        json.WriteBoolean("lowerTerrain", prefab.LowerTerrain);
        json.WriteBoolean("clipTerrain", prefab.ClipTerrain);
        json.WriteBoolean("followTerrain", prefab.FollowTerrain);
        json.WriteBoolean("flatJunctions", prefab.FlatJunctions);
        json.WriteEndObject();
        json.WriteStartObject("variants");
        WriteOptionalString(json, "elevated", prefab.ElevatedVariant);
        WriteOptionalString(json, "bridge", prefab.BridgeVariant);
        WriteOptionalString(json, "slope", prefab.SlopeVariant);
        WriteOptionalString(json, "tunnel", prefab.TunnelVariant);
        json.WriteEndObject();
        json.WriteStartArray("lanes");
        foreach (NetPrefabLane lane in prefab.Lanes)
        {
            json.WriteStartObject();
            json.WriteNumber("position", lane.Position);
            json.WriteNumber("width", lane.Width);
            json.WriteNumber("verticalOffset", lane.VerticalOffset);
            json.WriteNumber("stopOffset", lane.StopOffset);
            json.WriteNumber("speedLimit", lane.SpeedLimit);
            json.WriteNumber("direction", (int)lane.Direction);
            json.WriteNumber("finalDirection", (int)lane.FinalDirection);
            json.WriteNumber("laneType", (int)lane.LaneType);
            json.WriteNumber("vehicleType", lane.VehicleType);
            json.WriteNumber("stopType", lane.StopType);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    // Optional strings without a value are left out (docs/dump-format.md, manifest.json).
    private static void WriteOptionalString(JsonWriter json, string name, string? value)
    {
        if (value is not null)
        {
            json.WriteString(name, value);
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
