using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Trees;

/// <summary>
/// The tree prefabs the city's trees use, by their index in the game's prefab collection (docs/dump-format.md,
/// <c>tree-prefabs</c>; docs/research/cs1-assets.md, Prefabs).
/// </summary>
public sealed class CityTreePrefabs : ISectionData
{
    private readonly Dictionary<uint, TreePrefabRecord> _byIndex = [];

    /// <summary>Creates the prefabs.</summary>
    /// <param name="prefabs">The prefabs, ordered by index, each index once.</param>
    /// <exception cref="ArgumentException">An index is listed twice.</exception>
    public CityTreePrefabs(IReadOnlyList<TreePrefabRecord> prefabs)
    {
        ArgumentNullException.ThrowIfNull(prefabs);
        foreach (TreePrefabRecord prefab in prefabs)
        {
            if (!_byIndex.TryAdd(prefab.Index, prefab))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Prefab {prefab.Index} is listed twice."), nameof(prefabs));
            }
        }

        Prefabs = prefabs;
    }

    /// <summary>The prefabs, ordered by index.</summary>
    public IReadOnlyList<TreePrefabRecord> Prefabs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => [new("prefabs", Prefabs.Count.ToString(CultureInfo.InvariantCulture))];

    /// <inheritdoc/>
    /// <remarks>Prefabs have no position.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a prefab by its index.</summary>
    /// <param name="index">The index that trees refer to.</param>
    /// <param name="prefab">The prefab, if the dump has it.</param>
    public bool TryGet(uint index, [NotNullWhen(true)] out TreePrefabRecord? prefab) => _byIndex.TryGetValue(index, out prefab);
}
