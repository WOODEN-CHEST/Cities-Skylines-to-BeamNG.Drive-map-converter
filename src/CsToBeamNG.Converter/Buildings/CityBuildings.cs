using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>
/// The city's buildings, sub-buildings included, by id, in the game's coordinates (docs/dump-format.md,
/// <c>buildings</c>; docs/research/cs1-assets.md, Placement). They bound the area of interest with the buildings inside
/// the unlocked tiles that the map editor didn't place.
/// </summary>
public sealed class CityBuildings : ISectionData
{
    private readonly Dictionary<uint, BuildingRecord> _byId = [];
    private readonly CityBuildingPrefabs? _prefabs;

    /// <summary>Creates the buildings.</summary>
    /// <param name="buildings">The buildings, ordered by id, each id once, with finite values.</param>
    /// <param name="unlockedTiles">The city's unlocked tiles, which bound where it can build; empty if unknown.</param>
    /// <param name="prefabs">The prefabs, for the statistics by category; <see langword="null"/> if the dump has none.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityBuildings(IReadOnlyList<BuildingRecord> buildings, IReadOnlyList<TileCoordinate> unlockedTiles, CityBuildingPrefabs? prefabs)
    {
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(unlockedTiles);
        foreach (BuildingRecord building in buildings)
        {
            if (!_byId.TryAdd(building.Id, building))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Building {building.Id} is listed twice."), nameof(buildings));
            }
        }

        Buildings = buildings;
        _prefabs = prefabs;
        (AreaOfInterestBounds, ContentBuildings) = ContentBounds.WithinTiles(
            [.. buildings.Where(building => !building.HasFlags(BuildingRecord.OriginalFlag))],
            building => building.Position,
            unlockedTiles);
    }

    /// <summary>The buildings, ordered by id.</summary>
    public IReadOnlyList<BuildingRecord> Buildings { get; }

    /// <summary>
    /// The number of buildings that <see cref="AreaOfInterestBounds"/> encloses: those inside the unlocked tiles, or all
    /// of them if the unlocked tiles aren't known, except the map editor's.
    /// </summary>
    public int ContentBuildings { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>
    /// The bounds of the buildings' positions inside the unlocked tiles, like the networks' (docs/architecture.md, D35),
    /// leaving out what the map editor placed (<see cref="BuildingRecord.OriginalFlag"/>): boulders and rock formations
    /// decorate the map like its trees, also inside the unlocked tiles. Without unlocked tiles, the bounds of all the
    /// other buildings; <see langword="null"/> if there are none.
    /// </remarks>
    public MapRect? AreaOfInterestBounds { get; }

    /// <summary>Looks up a building by its id.</summary>
    /// <param name="id">The building's id.</param>
    /// <param name="building">The building, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out BuildingRecord? building) => _byId.TryGetValue(id, out building);

    private SectionStatistic[] BuildStatistics()
    {
        int subBuildings = 0;
        int notDrawn = 0;
        int notCompleted = 0;
        int abandoned = 0;
        int collapsed = 0;
        int[] categories = new int[Enum.GetValues<BuildingCategory>().Length];
        foreach (BuildingRecord building in Buildings)
        {
            subBuildings += building.ParentBuilding != 0 ? 1 : 0;
            notDrawn += building.IsDrawn ? 0 : 1;
            notCompleted += building.HasFlags(BuildingRecord.CompletedFlag) ? 0 : 1;
            abandoned += building.HasFlags(BuildingRecord.AbandonedFlag) ? 1 : 0;
            collapsed += building.HasFlags(BuildingRecord.CollapsedFlag) ? 1 : 0;
            categories[(int)(_prefabs?.CategoryOf(building.Prefab) ?? BuildingCategory.Other)]++;
        }

        return
        [
            new("buildings", string.Create(CultureInfo.InvariantCulture, $"{Buildings.Count}: {subBuildings} sub-buildings, {notDrawn} not drawn")),
            new("by category", _prefabs is null
                ? "unknown without the building-prefabs section"
                : string.Join(", ", BuildingCategories.InSummaryOrder.Select(category =>
                    string.Create(CultureInfo.InvariantCulture, $"{categories[(int)category]} {BuildingCategories.NameOf(category)}")))),
            new("states", string.Create(CultureInfo.InvariantCulture, $"{notCompleted} without the completed flag, {abandoned} abandoned, {collapsed} collapsed")),
            new("content", AreaOfInterestBounds is { } bounds
                ? string.Create(CultureInfo.InvariantCulture, $"{ContentBuildings} buildings within {bounds}")
                : "none"),
        ];
    }
}
