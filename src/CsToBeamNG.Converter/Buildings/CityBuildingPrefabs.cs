using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>
/// The building prefabs the city's buildings use, by their index in the game's prefab collection, each with its
/// <see cref="BuildingCategory"/> (docs/dump-format.md, <c>building-prefabs</c>; docs/research/cs1-assets.md, Prefabs).
/// </summary>
public sealed class CityBuildingPrefabs : ISectionData
{
    private readonly Dictionary<uint, BuildingPrefabRecord> _byIndex = [];
    private readonly Dictionary<uint, BuildingCategory> _categories = [];

    /// <summary>Creates the prefabs.</summary>
    /// <param name="prefabs">The prefabs, ordered by index, each index once.</param>
    /// <exception cref="ArgumentException">An index is listed twice.</exception>
    public CityBuildingPrefabs(IReadOnlyList<BuildingPrefabRecord> prefabs)
    {
        ArgumentNullException.ThrowIfNull(prefabs);
        foreach (BuildingPrefabRecord prefab in prefabs)
        {
            if (!_byIndex.TryAdd(prefab.Index, prefab))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Prefab {prefab.Index} is listed twice."), nameof(prefabs));
            }

            _categories.Add(prefab.Index, BuildingCategories.Of(prefab));
        }

        Prefabs = prefabs;
    }

    /// <summary>The prefabs, ordered by index.</summary>
    public IReadOnlyList<BuildingPrefabRecord> Prefabs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("prefabs", string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefabs.Count}, {Prefabs.Count(prefab => prefab.SubBuildings.Count > 0)} with sub-buildings")),
        new("by category", DescribeCategories()),
    ];

    /// <inheritdoc/>
    /// <remarks>Prefabs have no position.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a prefab by its index.</summary>
    /// <param name="index">The index that buildings refer to.</param>
    /// <param name="prefab">The prefab, if the dump has it.</param>
    public bool TryGet(uint index, [NotNullWhen(true)] out BuildingPrefabRecord? prefab) => _byIndex.TryGetValue(index, out prefab);

    /// <summary>Returns a prefab's category; <see cref="BuildingCategory.Other"/> for an index the dump doesn't have.</summary>
    /// <param name="index">The prefab's index.</param>
    public BuildingCategory CategoryOf(uint index) => _categories.TryGetValue(index, out BuildingCategory category) ? category : BuildingCategory.Other;

    private string DescribeCategories()
    {
        int[] counts = new int[Enum.GetValues<BuildingCategory>().Length];
        foreach (BuildingCategory category in _categories.Values)
        {
            counts[(int)category]++;
        }

        return string.Join(", ", BuildingCategories.InSummaryOrder.Select(category =>
            string.Create(CultureInfo.InvariantCulture, $"{counts[(int)category]} {BuildingCategories.NameOf(category)}")));
    }
}
