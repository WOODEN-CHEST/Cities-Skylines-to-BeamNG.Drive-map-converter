using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>Sorts building prefabs into <see cref="BuildingCategory"/>s and names the categories for summaries.</summary>
public static class BuildingCategories
{
    /// <summary>The categories in the order summaries list them, <see cref="BuildingCategory.Other"/> last.</summary>
    public static IReadOnlyList<BuildingCategory> InSummaryOrder { get; } =
        [BuildingCategory.Residential, BuildingCategory.Commercial, BuildingCategory.Industrial, BuildingCategory.Office, BuildingCategory.Other];

    /// <summary>
    /// Returns a prefab's category from its item class's service, as the game names it (docs/research/cs1-assets.md,
    /// Prefabs): <c>Residential</c>, <c>Commercial</c>, <c>Industrial</c> and <c>Office</c> are the zones' services.
    /// </summary>
    /// <param name="prefab">The prefab.</param>
    public static BuildingCategory Of(BuildingPrefabRecord prefab)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        return prefab.Class.Service switch
        {
            "Residential" => BuildingCategory.Residential,
            "Commercial" => BuildingCategory.Commercial,
            "Industrial" => BuildingCategory.Industrial,
            "Office" => BuildingCategory.Office,
            _ => BuildingCategory.Other,
        };
    }

    /// <summary>Returns a category's name in lowercase words, such as <c>residential</c>.</summary>
    /// <param name="category">The category.</param>
    public static string NameOf(BuildingCategory category) => category switch
    {
        BuildingCategory.Residential => "residential",
        BuildingCategory.Commercial => "commercial",
        BuildingCategory.Industrial => "industrial",
        BuildingCategory.Office => "office",
        BuildingCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown building category."),
    };
}
