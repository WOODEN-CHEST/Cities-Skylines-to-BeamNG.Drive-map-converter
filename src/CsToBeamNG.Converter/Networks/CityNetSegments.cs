using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// The city's network segments, by id, with the centre curves and corners the game computed (docs/dump-format.md,
/// <c>net-segments</c>; docs/research/cs1-networks.md, Data model and Corners).
/// </summary>
public sealed class CityNetSegments : ISectionData
{
    private readonly Dictionary<uint, NetSegmentRecord> _byId = [];
    private readonly CityNetPrefabs? _prefabs;

    /// <summary>Creates the segments.</summary>
    /// <param name="segments">The segments, ordered by id, each id once, with finite geometry.</param>
    /// <param name="prefabs">The prefabs, for the statistics by category; <see langword="null"/> if the dump has none.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityNetSegments(IReadOnlyList<NetSegmentRecord> segments, CityNetPrefabs? prefabs)
    {
        ArgumentNullException.ThrowIfNull(segments);
        foreach (NetSegmentRecord segment in segments)
        {
            if (!_byId.TryAdd(segment.Id, segment))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Segment {segment.Id} is listed twice."), nameof(segments));
            }
        }

        Segments = segments;
        _prefabs = prefabs;
    }

    /// <summary>The segments, ordered by id.</summary>
    public IReadOnlyList<NetSegmentRecord> Segments { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>The nodes bound the area of interest (<see cref="CityNetNodes.AreaOfInterestBounds"/>).</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a segment by its id.</summary>
    /// <param name="id">The segment's id.</param>
    /// <param name="segment">The segment, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out NetSegmentRecord? segment) => _byId.TryGetValue(id, out segment);

    private SectionStatistic[] BuildStatistics()
    {
        double[] meters = new double[Enum.GetValues<NetCategory>().Length];
        double total = 0;
        int oneWay = 0;
        foreach (NetSegmentRecord segment in Segments)
        {
            double length = NetGeometry.ArcLength(segment.Bezier);
            total += length;
            meters[(int)(_prefabs?.CategoryOf(segment.Prefab) ?? NetCategory.Other)] += length;
            if (_prefabs is not null && _prefabs.TryGet(segment.Prefab, out NetPrefabRecord? prefab)
                && NetDirections.OneWayOf(segment, prefab) != NetOneWay.None)
            {
                oneWay++;
            }
        }

        return
        [
            new("segments", string.Create(CultureInfo.InvariantCulture, $"{Segments.Count}, {total / 1000:0.##} km of centre lines")),
            new("by category", _prefabs is null
                ? "unknown without the net-prefabs section"
                : string.Join(", ", NetCategoryNames.InSummaryOrder.Select(category =>
                    string.Create(CultureInfo.InvariantCulture, $"{NetCategoryNames.Of(category)} {meters[(int)category] / 1000:0.##} km")))),
            new("one-way", _prefabs is null
                ? "unknown without the net-prefabs section"
                : string.Create(CultureInfo.InvariantCulture, $"{oneWay} segments")),
        ];
    }
}
