using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Props;

/// <summary>
/// The prop prefabs the city's props use, by their index in the game's prefab collection (docs/dump-format.md,
/// <c>prop-prefabs</c>; docs/research/cs1-assets.md, Prefabs).
/// </summary>
public sealed class CityPropPrefabs : ISectionData
{
    private readonly Dictionary<uint, PropPrefabRecord> _byIndex = [];

    /// <summary>Creates the prefabs.</summary>
    /// <param name="prefabs">The prefabs, ordered by index, each index once.</param>
    /// <exception cref="ArgumentException">An index is listed twice.</exception>
    public CityPropPrefabs(IReadOnlyList<PropPrefabRecord> prefabs)
    {
        ArgumentNullException.ThrowIfNull(prefabs);
        foreach (PropPrefabRecord prefab in prefabs)
        {
            if (!_byIndex.TryAdd(prefab.Index, prefab))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Prefab {prefab.Index} is listed twice."), nameof(prefabs));
            }
        }

        Prefabs = prefabs;
    }

    /// <summary>The prefabs, ordered by index.</summary>
    public IReadOnlyList<PropPrefabRecord> Prefabs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("prefabs", string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefabs.Count}: {Prefabs.Count(prefab => prefab.Decal)} decals, {Prefabs.Count(prefab => prefab.Marker)} markers, {Prefabs.Count(prefab => prefab.LightEffects)} with lights")),
    ];

    /// <inheritdoc/>
    /// <remarks>Prefabs have no position.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a prefab by its index.</summary>
    /// <param name="index">The index that props refer to.</param>
    /// <param name="prefab">The prefab, if the dump has it.</param>
    public bool TryGet(uint index, [NotNullWhen(true)] out PropPrefabRecord? prefab) => _byIndex.TryGetValue(index, out prefab);
}
