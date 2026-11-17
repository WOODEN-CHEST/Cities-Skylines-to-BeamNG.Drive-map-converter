using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Props;

/// <summary>Loads the <c>prop-prefabs</c> section into a <see cref="CityPropPrefabs"/> (docs/dump-format.md, <c>prop-prefabs</c>).</summary>
public sealed class PropPrefabsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => PropPrefabsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => PropPrefabsSection.Version;

    /// <inheritdoc/>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        PropPrefabsSectionContent content = PropPrefabsSectionReader.Read(section);
        return new CityPropPrefabs([.. content.Prefabs.OrderBy(prefab => prefab.Index)]);
    }
}
