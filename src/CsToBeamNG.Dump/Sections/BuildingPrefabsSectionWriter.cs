using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>building-prefabs</c> section (docs/dump-format.md, <c>building-prefabs</c>).</summary>
public static class BuildingPrefabsSectionWriter
{
    /// <summary>The largest number of cells a prefab's width or length can have in the file.</summary>
    public const int MaxCells = 255;

    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Prefabs are written ordered by
    /// index, whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The prefabs.</param>
    /// <exception cref="ArgumentException">A prefab is missing, listed twice or can't be written (<see cref="Check"/>).</exception>
    public static void Write(DumpWriter dump, BuildingPrefabsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<BuildingPrefabRecord> prefabs = RecordOrdering.SortUnique(content.Prefabs, prefab => prefab.Index, "Prefab", nameof(content));
        foreach (BuildingPrefabRecord prefab in prefabs)
        {
            string? problem = Check(prefab);
            if (problem is not null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Prefab {0} can't be written: {1}.", prefab.Index, problem),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(BuildingPrefabsSection.Name, BuildingPrefabsSection.Version);
        using (JsonWriter json = section.CreateJsonFile(BuildingPrefabsSection.PrefabsFileName))
        {
            json.WriteStartObject();
            json.WriteStartArray("prefabs");
            foreach (BuildingPrefabRecord prefab in prefabs)
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
    /// Returns why a prefab can't be written, such as a missing name or a size that isn't a finite number, or
    /// <see langword="null"/> if it can. The exporter uses it to skip such a prefab instead of failing the section.
    /// </summary>
    /// <param name="prefab">The prefab.</param>
    public static string? Check(BuildingPrefabRecord prefab)
    {
        Guard.NotNull(prefab, nameof(prefab));
        string? problem = PrefabJson.CheckNameAndClass(prefab.Name, prefab.Class);
        if (problem is not null)
        {
            return problem;
        }

        if (prefab.AiType is null || prefab.PlacementMode is null || prefab.ZoningMode is null)
        {
            return "its AI's type, placement mode or zoning mode is missing";
        }

        if (prefab.CellWidth is < 0 or > MaxCells || prefab.CellLength is < 0 or > MaxCells)
        {
            return string.Format(CultureInfo.InvariantCulture, "its size of {0} × {1} cells is out of range", prefab.CellWidth, prefab.CellLength);
        }

        if (!prefab.Size.IsFinite || !PrefabJson.IsFiniteOrAbsent(prefab.Mesh) || !PrefabJson.IsFiniteOrAbsent(prefab.LodMesh))
        {
            return "its size or the bounds of a mesh aren't finite numbers";
        }

        foreach (DumpColor color in prefab.Colors)
        {
            if (!color.IsFinite)
            {
                return "a colour isn't finite";
            }
        }

        for (int i = 0; i < prefab.SubBuildings.Count; i++)
        {
            BuildingSubBuilding sub = prefab.SubBuildings[i];
            if (sub is null || sub.Name is null)
            {
                return string.Format(CultureInfo.InvariantCulture, "sub-building {0} or its name is missing", i);
            }

            if (!sub.Position.IsFinite || !PrefabJson.IsFinite(sub.AngleDegrees))
            {
                return string.Format(CultureInfo.InvariantCulture, "the position or angle of sub-building {0} isn't finite", i);
            }
        }

        return null;
    }

    private static void WritePrefab(JsonWriter json, BuildingPrefabRecord prefab)
    {
        json.WriteStartObject();
        json.WriteNumber("index", prefab.Index);
        json.WriteString("name", prefab.Name);
        PrefabJson.WriteClass(json, prefab.Class);
        json.WriteStartObject("ai");
        json.WriteString("type", prefab.AiType);
        json.WriteEndObject();
        json.WriteNumber("cellWidth", prefab.CellWidth);
        json.WriteNumber("cellLength", prefab.CellLength);
        PrefabJson.WriteVector(json, "size", prefab.Size);
        PrefabJson.WriteOptionalBounds(json, "mesh", prefab.Mesh);
        PrefabJson.WriteOptionalBounds(json, "lodMesh", prefab.LodMesh);
        json.WriteString("placementMode", prefab.PlacementMode);
        json.WriteString("zoningMode", prefab.ZoningMode);
        json.WriteBoolean("circular", prefab.Circular);
        json.WriteBoolean("expandFrontYard", prefab.ExpandFrontYard);
        json.WriteStartObject("terrain");
        json.WriteBoolean("flattenTerrain", prefab.FlattenTerrain);
        json.WriteBoolean("flattenFullArea", prefab.FlattenFullArea);
        json.WriteBoolean("fullPavement", prefab.FullPavement);
        json.WriteBoolean("fullGravel", prefab.FullGravel);
        json.WriteBoolean("clipTerrain", prefab.ClipTerrain);
        json.WriteEndObject();
        json.WriteBoolean("requireHeightMap", prefab.RequireHeightMap);
        json.WriteBoolean("useColorVariations", prefab.UseColorVariations);
        json.WriteStartArray("colors");
        foreach (DumpColor color in prefab.Colors)
        {
            PrefabJson.WriteColorValue(json, color);
        }

        json.WriteEndArray();
        json.WriteStartArray("subBuildings");
        foreach (BuildingSubBuilding sub in prefab.SubBuildings)
        {
            json.WriteStartObject();
            if (sub.Prefab is { } index)
            {
                json.WriteNumber("prefab", index);
            }

            json.WriteString("name", sub.Name);
            PrefabJson.WriteVector(json, "position", sub.Position);
            json.WriteNumber("angle", sub.AngleDegrees);
            json.WriteBoolean("fixedHeight", sub.FixedHeight);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }
}
