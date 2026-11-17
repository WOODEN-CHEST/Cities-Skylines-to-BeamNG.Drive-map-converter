using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the city's buildings and their prefabs into the contents of the dump's <c>buildings</c> and
/// <c>building-prefabs</c> sections, without changing anything (docs/research/cs1-assets.md, Placement). Create it on
/// the main thread; read on the simulation thread, where the game changes the buildings.
/// </summary>
/// <remarks>
/// What the game derives for a building, where it draws the mesh and in which colour, comes from the game's own
/// methods, called as its renderer calls them. A building that can't be read is skipped and counted in the
/// <see cref="ObjectFailures"/> passed in.
/// </remarks>
internal sealed class BuildingReader
{
    private readonly BuildingManager _buildings;

    private BuildingReader(BuildingManager buildings) => _buildings = buildings;

    /// <summary>Finds the building manager. Main thread only (<see cref="CityReader.Require{T}"/>).</summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    public static BuildingReader Create() => new(CityReader.Require<BuildingManager>());

    /// <summary>Reads every created building, sub-buildings included. Simulation thread only.</summary>
    /// <param name="failures">Where to count the buildings that can't be read.</param>
    public BuildingsSectionContent ReadBuildings(ObjectFailures failures)
    {
        var content = new BuildingsSectionContent();
        Building[] buildings = _buildings.m_buildings.m_buffer;
        for (int i = 0; i < buildings.Length; i++)
        {
            if ((buildings[i].m_flags & Building.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                content.Buildings.Add(ReadBuilding(i, buildings[i]));
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<BuildingInfo>("Building", i, buildings[i].m_infoIndex) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>Reads the prefabs that created buildings use. Simulation thread only.</summary>
    /// <param name="failures">Where to count the prefabs that can't be read, such as a missing asset's.</param>
    public BuildingPrefabsSectionContent ReadPrefabs(ObjectFailures failures)
    {
        var content = new BuildingPrefabsSectionContent();
        foreach (ushort index in UsedPrefabIndices())
        {
            try
            {
                BuildingInfo info = PrefabCollection<BuildingInfo>.GetPrefab(index);
                if (info == null)
                {
                    failures.Add(PrefabFields.Describe<BuildingInfo>("Prefab", index, index) + " isn't loaded, so its buildings have no prefab.");
                    continue;
                }

                BuildingPrefabRecord record = ReadPrefab(index, info);
                string? problem = BuildingPrefabsSectionWriter.Check(record);
                if (problem is not null)
                {
                    failures.Add(PrefabFields.Describe<BuildingInfo>("Prefab", index, index) + " can't be written: " + problem + ".");
                    continue;
                }

                content.Prefabs.Add(record);
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<BuildingInfo>("Prefab", index, index) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    // The game's methods get a copy, so nothing they do can change the building.
    private static BuildingRecord ReadBuilding(int id, Building building)
    {
        BuildingInfo info = building.Info;
        if (info == null || info.m_buildingAI == null)
        {
            throw new InvalidOperationException("its prefab or the prefab's AI isn't loaded");
        }

        // The colour the game writes into its building colour map in the normal view (BuildingManager.UpdateColorMap
        // with InfoMode.None), and the mesh position its renderer uses (BuildingAI.RefreshInstance);
        // docs/research/cs1-assets.md, Placement.
        Color color = info.m_buildingAI.GetColor((ushort)id, ref building, InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
        Vector3 meshPosition = building.CalculateMeshPosition();
        return new BuildingRecord
        {
            Id = (uint)id,
            Flags = unchecked((uint)building.m_flags),
            Flags2 = unchecked((uint)building.m_flags2),
            Prefab = building.m_infoIndex,
            Position = PrefabFields.ToDump(building.m_position),
            Angle = building.m_angle,
            MeshPosition = PrefabFields.ToDump(meshPosition),
            ParentBuilding = building.m_parentBuilding,
            SubBuilding = building.m_subBuilding,
            Color = PrefabFields.ToDump(color),
            Width = building.m_width,
            Length = building.m_length,
            Level = building.m_level,
            BaseHeight = building.m_baseHeight,
        };
    }

    private static BuildingPrefabRecord ReadPrefab(ushort index, BuildingInfo info)
    {
        BuildingAI ai = info.m_buildingAI;
        BuildingInfoGen generated = info.m_generatedInfo;
        var record = new BuildingPrefabRecord
        {
            Index = index,
            Name = info.name ?? string.Empty,
            Class = PrefabFields.ToDump(info.m_class),
            AiType = ai != null ? ai.GetType().FullName ?? string.Empty : string.Empty,
            CellWidth = info.m_cellWidth,
            CellLength = info.m_cellLength,
            Size = PrefabFields.ToDump(info.m_size),

            // The extent the game computed from the mesh's vertices. The main mesh's own bounds aren't stable: the
            // renderer lowers them to the base heights of the buildings it draws (docs/research/cs1-assets.md, Prefabs).
            // BuildingInfoGen is a Unity object, so the check uses Unity's null.
            Mesh = generated == null ? null : new DumpBounds(PrefabFields.ToDump(generated!.m_min), PrefabFields.ToDump(generated.m_max)),
            LodMesh = PrefabFields.MeshBounds(info.m_lodMesh),
            PlacementMode = info.m_placementMode.ToString(),
            ZoningMode = info.m_zoningMode.ToString(),
            Circular = info.m_circular,
            ExpandFrontYard = info.m_expandFrontYard,
            FlattenTerrain = info.m_flattenTerrain,
            FlattenFullArea = info.m_flattenFullArea,
            FullPavement = info.m_fullPavement,
            FullGravel = info.m_fullGravel,
            ClipTerrain = info.m_clipTerrain,
            RequireHeightMap = info.m_requireHeightMap,
            UseColorVariations = info.m_useColorVariations,
        };
        record.Colors[0] = PrefabFields.ToDump(info.m_color0);
        record.Colors[1] = PrefabFields.ToDump(info.m_color1);
        record.Colors[2] = PrefabFields.ToDump(info.m_color2);
        record.Colors[3] = PrefabFields.ToDump(info.m_color3);
        foreach (BuildingInfo.SubInfo sub in info.m_subBuildings ?? [])
        {
            if (sub is null)
            {
                // Recorded as a hole, so the order stays right; the writer's check rejects the prefab.
                record.SubBuildings.Add(null!);
                continue;
            }

            BuildingInfo subInfo = sub.m_buildingInfo;
            bool loaded = subInfo != null;
            record.SubBuildings.Add(new BuildingSubBuilding
            {
                Prefab = loaded && subInfo!.m_prefabDataIndex >= 0 ? (uint)subInfo.m_prefabDataIndex : null,
                Name = loaded ? subInfo!.name ?? string.Empty : string.Empty,
                Position = PrefabFields.ToDump(sub.m_position),
                AngleDegrees = sub.m_angle,
                FixedHeight = sub.m_fixedHeight,
            });
        }

        return record;
    }

    private List<ushort> UsedPrefabIndices()
    {
        var used = new Dictionary<ushort, bool>();
        Building[] buildings = _buildings.m_buildings.m_buffer;
        for (int i = 0; i < buildings.Length; i++)
        {
            if ((buildings[i].m_flags & Building.Flags.Created) != 0)
            {
                used[buildings[i].m_infoIndex] = true;
            }
        }

        var indices = new List<ushort>(used.Keys);
        indices.Sort();
        return indices;
    }
}
