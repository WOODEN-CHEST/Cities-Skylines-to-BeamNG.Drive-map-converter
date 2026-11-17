#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>prop-prefabs</c> section written by <see cref="PropPrefabsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class PropPrefabsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="PropPrefabsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged, or lists a prefab index twice.</exception>
    public static PropPrefabsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string path = section.GetFilePath(PropPrefabsSection.PrefabsFileName);
        using JsonDocument document = section.ReadJson(PropPrefabsSection.PrefabsFileName);
        var json = new JsonFieldReader(path);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        var content = new PropPrefabsSectionContent();
        var indices = new HashSet<uint>();
        foreach ((JsonElement item, string itemPath) in json.RequiredArray(root, "prefabs", "$"))
        {
            PropPrefabRecord prefab = ReadPrefab(json, item, itemPath);
            if (!indices.Add(prefab.Index))
            {
                throw json.Error(itemPath, string.Create(CultureInfo.InvariantCulture, $"prefab index {prefab.Index} is listed twice"));
            }

            content.Prefabs.Add(prefab);
        }

        return content;
    }

    private static PropPrefabRecord ReadPrefab(JsonFieldReader json, JsonElement item, string path)
    {
        json.RequireObject(item, path);
        var prefab = new PropPrefabRecord
        {
            Index = json.RequiredUInt32(item, "index", path),
            Name = json.RequiredString(item, "name", path),
            Class = PrefabJsonReader.ReadClass(json, item, path),
            Mesh = PrefabJsonReader.ReadOptionalBounds(json, item, "mesh", path),
            LodMesh = PrefabJsonReader.ReadOptionalBounds(json, item, "lodMesh", path),
            MinScale = json.RequiredSingle(item, "minScale", path),
            MaxScale = json.RequiredSingle(item, "maxScale", path),
            Decal = json.RequiredBoolean(item, "decal", path),
            Marker = json.RequiredBoolean(item, "marker", path),
            RequireHeightMap = json.RequiredBoolean(item, "requireHeightMap", path),
            RequireWaterMap = json.RequiredBoolean(item, "requireWaterMap", path),
            CreateRuining = json.RequiredBoolean(item, "createRuining", path),
            LightEffects = json.RequiredBoolean(item, "lightEffects", path),
            UseColorVariations = json.RequiredBoolean(item, "useColorVariations", path),
        };
        PrefabJsonReader.ReadColors(json, item, "colors", path, prefab.Colors);
        return prefab;
    }
}
#endif
