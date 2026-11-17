using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>Loads the <c>net-prefabs</c> section into a <see cref="CityNetPrefabs"/> (docs/dump-format.md, <c>net-prefabs</c>).</summary>
public sealed class NetPrefabsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => NetPrefabsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => NetPrefabsSection.Version;

    /// <inheritdoc/>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        NetPrefabsSectionContent content = NetPrefabsSectionReader.Read(section);
        return new CityNetPrefabs([.. content.Prefabs.OrderBy(prefab => prefab.Index)]);
    }
}
