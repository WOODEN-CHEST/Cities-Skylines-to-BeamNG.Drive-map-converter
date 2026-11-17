using System;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>Loads the <c>terrain</c> section into a <see cref="CityTerrain"/> (docs/dump-format.md, <c>terrain</c>).</summary>
public sealed class TerrainLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => TerrainSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => TerrainSection.Version;

    /// <inheritdoc/>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        var terrain = new CityTerrain(TerrainSectionReader.Read(section));
        if (terrain.DetailTiles.Count == 0)
        {
            context.Inform("The terrain has no 4 m detail heights, so the ground is known only on the 16 m grid.");
        }

        return terrain;
    }
}
