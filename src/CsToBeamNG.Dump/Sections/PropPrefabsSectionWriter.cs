using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>prop-prefabs</c> section (docs/dump-format.md, <c>prop-prefabs</c>).</summary>
public static class PropPrefabsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Prefabs are written ordered by
    /// index, whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The prefabs.</param>
    /// <exception cref="ArgumentException">A prefab is missing, listed twice or can't be written (<see cref="Check"/>).</exception>
    public static void Write(DumpWriter dump, PropPrefabsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<PropPrefabRecord> prefabs = RecordOrdering.SortUnique(content.Prefabs, prefab => prefab.Index, "Prefab", nameof(content));
        foreach (PropPrefabRecord prefab in prefabs)
        {
            string? problem = Check(prefab);
            if (problem is not null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Prefab {0} can't be written: {1}.", prefab.Index, problem),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(PropPrefabsSection.Name, PropPrefabsSection.Version);
        using (JsonWriter json = section.CreateJsonFile(PropPrefabsSection.PrefabsFileName))
        {
            json.WriteStartObject();
            json.WriteStartArray("prefabs");
            foreach (PropPrefabRecord prefab in prefabs)
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
    /// Returns why a prefab can't be written, such as a missing name or a scale that isn't a finite number, or
    /// <see langword="null"/> if it can. The exporter uses it to skip such a prefab instead of failing the section.
    /// </summary>
    /// <param name="prefab">The prefab.</param>
    public static string? Check(PropPrefabRecord prefab)
    {
        Guard.NotNull(prefab, nameof(prefab));
        string? problem = PrefabJson.CheckNameAndClass(prefab.Name, prefab.Class);
        if (problem is not null)
        {
            return problem;
        }

        if (!PrefabJson.IsFinite(prefab.MinScale) || !PrefabJson.IsFinite(prefab.MaxScale)
            || !PrefabJson.IsFiniteOrAbsent(prefab.Mesh) || !PrefabJson.IsFiniteOrAbsent(prefab.LodMesh))
        {
            return "its scales or the bounds of a mesh aren't finite numbers";
        }

        foreach (DumpColor color in prefab.Colors)
        {
            if (!color.IsFinite)
            {
                return "a colour isn't finite";
            }
        }

        return null;
    }

    private static void WritePrefab(JsonWriter json, PropPrefabRecord prefab)
    {
        json.WriteStartObject();
        json.WriteNumber("index", prefab.Index);
        json.WriteString("name", prefab.Name);
        PrefabJson.WriteClass(json, prefab.Class);
        PrefabJson.WriteOptionalBounds(json, "mesh", prefab.Mesh);
        PrefabJson.WriteOptionalBounds(json, "lodMesh", prefab.LodMesh);
        json.WriteNumber("minScale", prefab.MinScale);
        json.WriteNumber("maxScale", prefab.MaxScale);
        json.WriteBoolean("decal", prefab.Decal);
        json.WriteBoolean("marker", prefab.Marker);
        json.WriteBoolean("requireHeightMap", prefab.RequireHeightMap);
        json.WriteBoolean("requireWaterMap", prefab.RequireWaterMap);
        json.WriteBoolean("createRuining", prefab.CreateRuining);
        json.WriteBoolean("lightEffects", prefab.LightEffects);
        json.WriteBoolean("useColorVariations", prefab.UseColorVariations);
        json.WriteStartArray("colors");
        foreach (DumpColor color in prefab.Colors)
        {
            PrefabJson.WriteColorValue(json, color);
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }
}
