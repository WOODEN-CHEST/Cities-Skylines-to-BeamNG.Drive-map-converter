#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>net-prefabs</c> section written by <see cref="NetPrefabsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class NetPrefabsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="NetPrefabsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged, or lists a prefab index twice.</exception>
    public static NetPrefabsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string path = section.GetFilePath(NetPrefabsSection.PrefabsFileName);
        using JsonDocument document = section.ReadJson(NetPrefabsSection.PrefabsFileName);
        var json = new JsonFieldReader(path);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        var content = new NetPrefabsSectionContent();
        var indices = new HashSet<uint>();
        foreach ((JsonElement item, string itemPath) in json.RequiredArray(root, "prefabs", "$"))
        {
            NetPrefabRecord prefab = ReadPrefab(json, item, itemPath);
            if (!indices.Add(prefab.Index))
            {
                throw json.Error(itemPath, string.Create(CultureInfo.InvariantCulture, $"prefab index {prefab.Index} is listed twice"));
            }

            content.Prefabs.Add(prefab);
        }

        return content;
    }

    private static NetPrefabRecord ReadPrefab(JsonFieldReader json, JsonElement item, string path)
    {
        json.RequireObject(item, path);
        JsonElement itemClass = json.RequiredObject(item, "class", path);
        string classPath = JsonFieldReader.Child(path, "class");
        JsonElement ai = json.RequiredObject(item, "ai", path);
        string aiPath = JsonFieldReader.Child(path, "ai");
        JsonElement terrain = json.RequiredObject(item, "terrain", path);
        string terrainPath = JsonFieldReader.Child(path, "terrain");
        JsonElement variants = json.RequiredObject(item, "variants", path);
        string variantsPath = JsonFieldReader.Child(path, "variants");
        var prefab = new NetPrefabRecord
        {
            Index = json.RequiredUInt32(item, "index", path),
            Name = json.RequiredString(item, "name", path),
            ClassName = json.RequiredString(itemClass, "name", classPath),
            Service = json.RequiredString(itemClass, "service", classPath),
            SubService = json.RequiredString(itemClass, "subService", classPath),
            Level = json.RequiredString(itemClass, "level", classPath),
            Layer = json.RequiredUInt32(itemClass, "layer", classPath),
            AiType = json.RequiredString(ai, "type", aiPath),
            Highway = json.RequiredBoolean(ai, "highway", aiPath),
            Bridge = json.RequiredBoolean(ai, "bridge", aiPath),
            Tunnel = json.RequiredBoolean(ai, "tunnel", aiPath),
            Underground = json.RequiredBoolean(ai, "underground", aiPath),
            Overground = json.RequiredBoolean(ai, "overground", aiPath),
            HalfWidth = json.RequiredSingle(item, "halfWidth", path),
            PavementWidth = json.RequiredSingle(item, "pavementWidth", path),
            SegmentLength = json.RequiredSingle(item, "segmentLength", path),
            SurfaceLevel = json.RequiredSingle(item, "surfaceLevel", path),
            CreatePavement = json.RequiredBoolean(terrain, "createPavement", terrainPath),
            CreateGravel = json.RequiredBoolean(terrain, "createGravel", terrainPath),
            CreateRuining = json.RequiredBoolean(terrain, "createRuining", terrainPath),
            FlattenTerrain = json.RequiredBoolean(terrain, "flattenTerrain", terrainPath),
            LowerTerrain = json.RequiredBoolean(terrain, "lowerTerrain", terrainPath),
            ClipTerrain = json.RequiredBoolean(terrain, "clipTerrain", terrainPath),
            FollowTerrain = json.RequiredBoolean(terrain, "followTerrain", terrainPath),
            FlatJunctions = json.RequiredBoolean(terrain, "flatJunctions", terrainPath),
            ElevatedVariant = json.OptionalString(variants, "elevated", variantsPath),
            BridgeVariant = json.OptionalString(variants, "bridge", variantsPath),
            SlopeVariant = json.OptionalString(variants, "slope", variantsPath),
            TunnelVariant = json.OptionalString(variants, "tunnel", variantsPath),
        };
        foreach ((JsonElement lane, string lanePath) in json.RequiredArray(item, "lanes", path))
        {
            json.RequireObject(lane, lanePath);
            prefab.Lanes.Add(new NetPrefabLane
            {
                Position = json.RequiredSingle(lane, "position", lanePath),
                Width = json.RequiredSingle(lane, "width", lanePath),
                VerticalOffset = json.RequiredSingle(lane, "verticalOffset", lanePath),
                StopOffset = json.RequiredSingle(lane, "stopOffset", lanePath),
                SpeedLimit = json.RequiredSingle(lane, "speedLimit", lanePath),
                Direction = (byte)json.RequiredInt32(lane, "direction", lanePath, 0, byte.MaxValue),
                FinalDirection = (byte)json.RequiredInt32(lane, "finalDirection", lanePath, 0, byte.MaxValue),
                LaneType = (byte)json.RequiredInt32(lane, "laneType", lanePath, 0, byte.MaxValue),
                VehicleType = json.RequiredUInt32(lane, "vehicleType", lanePath),
                StopType = json.RequiredUInt32(lane, "stopType", lanePath),
            });
        }

        return prefab;
    }
}
#endif
