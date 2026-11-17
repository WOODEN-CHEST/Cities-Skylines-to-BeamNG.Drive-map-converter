using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>Loads the <c>building-prefabs</c> section into a <see cref="CityBuildingPrefabs"/> (docs/dump-format.md, <c>building-prefabs</c>).</summary>
public sealed class BuildingPrefabsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => BuildingPrefabsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => BuildingPrefabsSection.Version;

    /// <inheritdoc/>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        BuildingPrefabsSectionContent content = BuildingPrefabsSectionReader.Read(section);
        return new CityBuildingPrefabs([.. content.Prefabs.OrderBy(prefab => prefab.Index)]);
    }
}
