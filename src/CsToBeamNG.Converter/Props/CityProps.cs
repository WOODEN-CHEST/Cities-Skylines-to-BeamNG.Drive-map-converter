using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Props;

/// <summary>
/// The props placed on their own, by id, in the game's coordinates (docs/dump-format.md, <c>props</c>;
/// docs/research/cs1-assets.md, Placement).
/// </summary>
public sealed class CityProps : ISectionData
{
    private readonly Dictionary<uint, PropRecord> _byId = [];
    private readonly CityPropPrefabs? _prefabs;

    /// <summary>Creates the props.</summary>
    /// <param name="props">The props, ordered by id, each id once, with finite values.</param>
    /// <param name="prefabs">The prefabs, for the statistics; <see langword="null"/> if the dump has none.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityProps(IReadOnlyList<PropRecord> props, CityPropPrefabs? prefabs)
    {
        ArgumentNullException.ThrowIfNull(props);
        foreach (PropRecord prop in props)
        {
            if (!_byId.TryAdd(prop.Id, prop))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Prop {prop.Id} is listed twice."), nameof(props));
            }
        }

        Props = props;
        _prefabs = prefabs;
    }

    /// <summary>The props, ordered by id.</summary>
    public IReadOnlyList<PropRecord> Props { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>
    /// Props don't bound the area of interest (docs/architecture.md, D35): those the player placed stand among the
    /// buildings and roads, and the map editor may have put others anywhere on the map.
    /// </remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a prop by its id.</summary>
    /// <param name="id">The prop's id.</param>
    /// <param name="prop">The prop, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out PropRecord? prop) => _byId.TryGetValue(id, out prop);

    private SectionStatistic[] BuildStatistics()
    {
        int notDrawn = 0;
        int fixedHeight = 0;
        int decals = 0;
        float minScale = float.PositiveInfinity;
        float maxScale = float.NegativeInfinity;
        foreach (PropRecord prop in Props)
        {
            notDrawn += prop.IsDrawn ? 0 : 1;
            fixedHeight += prop.HasFlags(PropRecord.FixedHeightFlag) ? 1 : 0;
            decals += _prefabs is not null && _prefabs.TryGet(prop.Prefab, out PropPrefabRecord? prefab) && prefab.Decal ? 1 : 0;
            minScale = Math.Min(minScale, prop.Scale);
            maxScale = Math.Max(maxScale, prop.Scale);
        }

        return
        [
            new("props", string.Create(CultureInfo.InvariantCulture, $"{Props.Count}: {notDrawn} not drawn, {fixedHeight} with a fixed height")),
            new("decals", _prefabs is null
                ? "unknown without the prop-prefabs section"
                : decals.ToString(CultureInfo.InvariantCulture)),
            new("scales", Props.Count == 0
                ? "none"
                : string.Create(CultureInfo.InvariantCulture, $"{minScale:0.###} to {maxScale:0.###}")),
        ];
    }
}
