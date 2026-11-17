#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>tree-prefabs</c> section written by <see cref="TreePrefabsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class TreePrefabsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="TreePrefabsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged, or lists a prefab index twice.</exception>
    public static TreePrefabsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string path = section.GetFilePath(TreePrefabsSection.PrefabsFileName);
        using JsonDocument document = section.ReadJson(TreePrefabsSection.PrefabsFileName);
        var json = new JsonFieldReader(path);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        var content = new TreePrefabsSectionContent();
        var indices = new HashSet<uint>();
        foreach ((JsonElement item, string itemPath) in json.RequiredArray(root, "prefabs", "$"))
        {
            json.RequireObject(item, itemPath);
            var prefab = new TreePrefabRecord
            {
                Index = json.RequiredUInt32(item, "index", itemPath),
                Name = json.RequiredString(item, "name", itemPath),
                Class = PrefabJsonReader.ReadClass(json, item, itemPath),
                Mesh = PrefabJsonReader.ReadOptionalBounds(json, item, "mesh", itemPath),
                MinScale = json.RequiredSingle(item, "minScale", itemPath),
                MaxScale = json.RequiredSingle(item, "maxScale", itemPath),
                MinBrightness = json.RequiredSingle(item, "minBrightness", itemPath),
                MaxBrightness = json.RequiredSingle(item, "maxBrightness", itemPath),
                DefaultColor = PrefabJsonReader.ReadColor(json, item, "defaultColor", itemPath),
                CreateRuining = json.RequiredBoolean(item, "createRuining", itemPath),
            };
            if (!indices.Add(prefab.Index))
            {
                throw json.Error(itemPath, string.Create(CultureInfo.InvariantCulture, $"prefab index {prefab.Index} is listed twice"));
            }

            content.Prefabs.Add(prefab);
        }

        return content;
    }
}
#endif
