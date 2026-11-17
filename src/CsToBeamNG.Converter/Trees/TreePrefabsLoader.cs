using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Trees;

/// <summary>Loads the <c>tree-prefabs</c> section into a <see cref="CityTreePrefabs"/> (docs/dump-format.md, <c>tree-prefabs</c>).</summary>
public sealed class TreePrefabsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => TreePrefabsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => TreePrefabsSection.Version;

    /// <inheritdoc/>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        TreePrefabsSectionContent content = TreePrefabsSectionReader.Read(section);
        return new CityTreePrefabs([.. content.Prefabs.OrderBy(prefab => prefab.Index)]);
    }
}
