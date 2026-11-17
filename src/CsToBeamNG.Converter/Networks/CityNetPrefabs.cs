using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// The network prefabs the city's nodes and segments use, by their index in the game's prefab collection, each with
/// its <see cref="NetCategory"/> (docs/dump-format.md, <c>net-prefabs</c>; docs/research/cs1-networks.md, Prefabs).
/// </summary>
public sealed class CityNetPrefabs : ISectionData
{
    private readonly Dictionary<uint, NetPrefabRecord> _byIndex = [];
    private readonly Dictionary<uint, NetCategory> _categories = [];

    /// <summary>Creates the prefabs.</summary>
    /// <param name="prefabs">The prefabs, ordered by index, each index once.</param>
    /// <exception cref="ArgumentException">An index is listed twice.</exception>
    public CityNetPrefabs(IReadOnlyList<NetPrefabRecord> prefabs)
    {
        ArgumentNullException.ThrowIfNull(prefabs);
        foreach (NetPrefabRecord prefab in prefabs)
        {
            if (!_byIndex.TryAdd(prefab.Index, prefab))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Prefab {prefab.Index} is listed twice."), nameof(prefabs));
            }

            _categories.Add(prefab.Index, NetCategories.Of(prefab));
        }

        Prefabs = prefabs;
    }

    /// <summary>The prefabs, ordered by index.</summary>
    public IReadOnlyList<NetPrefabRecord> Prefabs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("prefabs", string.Create(CultureInfo.InvariantCulture, $"{Prefabs.Count}, with {Prefabs.Sum(prefab => prefab.Lanes.Count)} lanes")),
        new("by category", DescribeCategories()),
    ];

    /// <inheritdoc/>
    /// <remarks>Prefabs have no position.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a prefab by its index.</summary>
    /// <param name="index">The index that nodes and segments refer to.</param>
    /// <param name="prefab">The prefab, if the dump has it.</param>
    public bool TryGet(uint index, [NotNullWhen(true)] out NetPrefabRecord? prefab) => _byIndex.TryGetValue(index, out prefab);

    /// <summary>Returns a prefab's category; <see cref="NetCategory.Other"/> for an index the dump doesn't have.</summary>
    /// <param name="index">The prefab's index.</param>
    public NetCategory CategoryOf(uint index) => _categories.TryGetValue(index, out NetCategory category) ? category : NetCategory.Other;

    private string DescribeCategories()
    {
        int[] counts = new int[Enum.GetValues<NetCategory>().Length];
        foreach (NetCategory category in _categories.Values)
        {
            counts[(int)category]++;
        }

        return string.Join(", ", NetCategoryNames.InSummaryOrder.Select(category =>
            string.Create(CultureInfo.InvariantCulture, $"{counts[(int)category]} {NetCategoryNames.Of(category)}")));
    }
}
