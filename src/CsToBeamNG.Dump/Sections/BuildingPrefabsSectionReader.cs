#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>building-prefabs</c> section written by <see cref="BuildingPrefabsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class BuildingPrefabsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="BuildingPrefabsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged, or lists a prefab index twice.</exception>
    public static BuildingPrefabsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string path = section.GetFilePath(BuildingPrefabsSection.PrefabsFileName);
        using JsonDocument document = section.ReadJson(BuildingPrefabsSection.PrefabsFileName);
        var json = new JsonFieldReader(path);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        var content = new BuildingPrefabsSectionContent();
        var indices = new HashSet<uint>();
        foreach ((JsonElement item, string itemPath) in json.RequiredArray(root, "prefabs", "$"))
        {
            BuildingPrefabRecord prefab = ReadPrefab(json, item, itemPath);
            if (!indices.Add(prefab.Index))
            {
                throw json.Error(itemPath, string.Create(CultureInfo.InvariantCulture, $"prefab index {prefab.Index} is listed twice"));
            }

            content.Prefabs.Add(prefab);
        }

        return content;
    }

    private static BuildingPrefabRecord ReadPrefab(JsonFieldReader json, JsonElement item, string path)
    {
        json.RequireObject(item, path);
        JsonElement ai = json.RequiredObject(item, "ai", path);
        JsonElement terrain = json.RequiredObject(item, "terrain", path);
        string terrainPath = JsonFieldReader.Child(path, "terrain");
        var prefab = new BuildingPrefabRecord
        {
            Index = json.RequiredUInt32(item, "index", path),
            Name = json.RequiredString(item, "name", path),
            Class = PrefabJsonReader.ReadClass(json, item, path),
            AiType = json.RequiredString(ai, "type", JsonFieldReader.Child(path, "ai")),
            CellWidth = json.RequiredInt32(item, "cellWidth", path, 0, BuildingPrefabsSectionWriter.MaxCells),
            CellLength = json.RequiredInt32(item, "cellLength", path, 0, BuildingPrefabsSectionWriter.MaxCells),
            Size = PrefabJsonReader.ReadVector(json, item, "size", path),
            Mesh = PrefabJsonReader.ReadOptionalBounds(json, item, "mesh", path),
            LodMesh = PrefabJsonReader.ReadOptionalBounds(json, item, "lodMesh", path),
            PlacementMode = json.RequiredString(item, "placementMode", path),
            ZoningMode = json.RequiredString(item, "zoningMode", path),
            Circular = json.RequiredBoolean(item, "circular", path),
            ExpandFrontYard = json.RequiredBoolean(item, "expandFrontYard", path),
            FlattenTerrain = json.RequiredBoolean(terrain, "flattenTerrain", terrainPath),
            FlattenFullArea = json.RequiredBoolean(terrain, "flattenFullArea", terrainPath),
            FullPavement = json.RequiredBoolean(terrain, "fullPavement", terrainPath),
            FullGravel = json.RequiredBoolean(terrain, "fullGravel", terrainPath),
            ClipTerrain = json.RequiredBoolean(terrain, "clipTerrain", terrainPath),
            RequireHeightMap = json.RequiredBoolean(item, "requireHeightMap", path),
            UseColorVariations = json.RequiredBoolean(item, "useColorVariations", path),
        };
        PrefabJsonReader.ReadColors(json, item, "colors", path, prefab.Colors);
        foreach ((JsonElement sub, string subPath) in json.RequiredArray(item, "subBuildings", path))
        {
            json.RequireObject(sub, subPath);
            prefab.SubBuildings.Add(new BuildingSubBuilding
            {
                Prefab = json.OptionalUInt32(sub, "prefab", subPath),
                Name = json.RequiredString(sub, "name", subPath),
                Position = PrefabJsonReader.ReadVector(json, sub, "position", subPath),
                AngleDegrees = json.RequiredSingle(sub, "angle", subPath),
                FixedHeight = json.RequiredBoolean(sub, "fixedHeight", subPath),
            });
        }

        return prefab;
    }
}
#endif
