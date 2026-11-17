using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>tree-prefabs</c> section (docs/dump-format.md, <c>tree-prefabs</c>).</summary>
public static class TreePrefabsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Prefabs are written ordered by
    /// index, whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The prefabs.</param>
    /// <exception cref="ArgumentException">A prefab is missing, listed twice or can't be written (<see cref="Check"/>).</exception>
    public static void Write(DumpWriter dump, TreePrefabsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<TreePrefabRecord> prefabs = RecordOrdering.SortUnique(content.Prefabs, prefab => prefab.Index, "Prefab", nameof(content));
        foreach (TreePrefabRecord prefab in prefabs)
        {
            string? problem = Check(prefab);
            if (problem is not null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Prefab {0} can't be written: {1}.", prefab.Index, problem),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(TreePrefabsSection.Name, TreePrefabsSection.Version);
        using (JsonWriter json = section.CreateJsonFile(TreePrefabsSection.PrefabsFileName))
        {
            json.WriteStartObject();
            json.WriteStartArray("prefabs");
            foreach (TreePrefabRecord prefab in prefabs)
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
    public static string? Check(TreePrefabRecord prefab)
    {
        Guard.NotNull(prefab, nameof(prefab));
        string? problem = PrefabJson.CheckNameAndClass(prefab.Name, prefab.Class);
        if (problem is not null)
        {
            return problem;
        }

        if (!PrefabJson.IsFinite(prefab.MinScale) || !PrefabJson.IsFinite(prefab.MaxScale)
            || !PrefabJson.IsFinite(prefab.MinBrightness) || !PrefabJson.IsFinite(prefab.MaxBrightness)
            || !prefab.DefaultColor.IsFinite || !PrefabJson.IsFiniteOrAbsent(prefab.Mesh))
        {
            return "its scales, brightness, default colour or mesh bounds aren't finite numbers";
        }

        return null;
    }

    private static void WritePrefab(JsonWriter json, TreePrefabRecord prefab)
    {
        json.WriteStartObject();
        json.WriteNumber("index", prefab.Index);
        json.WriteString("name", prefab.Name);
        PrefabJson.WriteClass(json, prefab.Class);
        PrefabJson.WriteOptionalBounds(json, "mesh", prefab.Mesh);
        json.WriteNumber("minScale", prefab.MinScale);
        json.WriteNumber("maxScale", prefab.MaxScale);
        json.WriteNumber("minBrightness", prefab.MinBrightness);
        json.WriteNumber("maxBrightness", prefab.MaxBrightness);
        PrefabJson.WriteColor(json, "defaultColor", prefab.DefaultColor);
        json.WriteBoolean("createRuining", prefab.CreateRuining);
        json.WriteEndObject();
    }
}
