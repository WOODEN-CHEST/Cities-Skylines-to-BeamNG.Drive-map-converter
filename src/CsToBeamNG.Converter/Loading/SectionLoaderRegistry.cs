using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Converter.Buildings;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Props;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Trees;
using CsToBeamNG.Converter.Water;

namespace CsToBeamNG.Converter.Loading;

/// <summary>
/// The section loaders the converter knows, in the order they run. A loader can use the data of the sections
/// loaded before it (<see cref="SectionLoadContext.Find{T}"/>), so register dependencies first.
/// </summary>
public sealed class SectionLoaderRegistry
{
    private readonly List<ISectionLoader> _loaders = [];

    /// <summary>The registered loaders, in the order they run.</summary>
    public IReadOnlyList<ISectionLoader> Loaders => _loaders;

    /// <summary>Creates the registry with every loader the converter has.</summary>
    /// <remarks>
    /// One loader per section of docs/dump-format.md, Sections: terrain, water, the networks, buildings, props and trees.
    /// Loaders check references against the sections loaded before them, so prefabs load before the objects that use
    /// them, and the networks run prefabs, nodes, segments, lanes.
    /// </remarks>
    public static SectionLoaderRegistry CreateDefault() => new SectionLoaderRegistry()
        .Add(new TerrainLoader())
        .Add(new WaterLoader())
        .Add(new NetPrefabsLoader())
        .Add(new NetNodesLoader())
        .Add(new NetSegmentsLoader())
        .Add(new NetLanesLoader())
        .Add(new BuildingPrefabsLoader())
        .Add(new BuildingsLoader())
        .Add(new PropPrefabsLoader())
        .Add(new PropsLoader())
        .Add(new TreePrefabsLoader())
        .Add(new TreesLoader());

    /// <summary>Registers a loader; it runs after the ones registered before it.</summary>
    /// <param name="loader">The loader.</param>
    /// <returns>This registry, so calls can be chained.</returns>
    /// <exception cref="ArgumentException">A loader for the same section is registered already.</exception>
    public SectionLoaderRegistry Add(ISectionLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        if (Find(loader.SectionName) is not null)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"A loader for section '{loader.SectionName}' is registered already."),
                nameof(loader));
        }

        _loaders.Add(loader);
        return this;
    }

    /// <summary>Returns the loader of a section, or <see langword="null"/> if there is none.</summary>
    /// <param name="sectionName">The section's name.</param>
    public ISectionLoader? Find(string sectionName)
    {
        ArgumentNullException.ThrowIfNull(sectionName);
        foreach (ISectionLoader loader in _loaders)
        {
            if (string.Equals(loader.SectionName, sectionName, StringComparison.Ordinal))
            {
                return loader;
            }
        }

        return null;
    }
}
