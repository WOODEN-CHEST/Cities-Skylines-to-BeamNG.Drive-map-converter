using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>Loads the <c>buildings</c> section into a <see cref="CityBuildings"/> (docs/dump-format.md, <c>buildings</c>).</summary>
public sealed class BuildingsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => BuildingsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => BuildingsSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A building with a value that isn't finite, or whose id was listed already, is skipped with a warning. Buildings
    /// whose prefab the <c>building-prefabs</c> section lacks, or whose parent building this section lacks, are kept
    /// and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        BuildingsSectionContent content = BuildingsSectionReader.Read(section);
        CityBuildingPrefabs? prefabs = context.Find<CityBuildingPrefabs>();
        var unknownPrefabs = new MissingReferences("buildings have a prefab that the building-prefabs section lacks");
        var buildings = new List<BuildingRecord>(content.Buildings.Count);
        var ids = new HashSet<uint>();
        foreach (BuildingRecord building in content.Buildings.OrderBy(building => building.Id))
        {
            if (!building.Position.IsFinite || !float.IsFinite(building.Angle) || !building.MeshPosition.IsFinite || !building.Color.IsFinite)
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Building {building.Id} has a position, angle or colour that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(building.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Building {building.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (prefabs is not null && !prefabs.TryGet(building.Prefab, out _))
            {
                unknownPrefabs.Add(string.Create(CultureInfo.InvariantCulture, $"building {building.Id} (prefab {building.Prefab})"));
            }

            buildings.Add(building);
        }

        var unknownParents = new MissingReferences("sub-buildings belong to a building that the buildings section lacks");
        foreach (BuildingRecord building in buildings)
        {
            if (building.ParentBuilding != 0 && !ids.Contains(building.ParentBuilding))
            {
                unknownParents.Add(string.Create(CultureInfo.InvariantCulture, $"building {building.Id} (parent {building.ParentBuilding})"));
            }
        }

        unknownPrefabs.Report(context);
        unknownParents.Report(context);
        return new CityBuildings(buildings, [.. context.Manifest.City.UnlockedTiles], prefabs);
    }
}
