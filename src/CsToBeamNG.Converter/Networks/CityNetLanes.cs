using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// The lanes of the city's network segments, by id (docs/dump-format.md, <c>net-lanes</c>; docs/research/cs1-networks.md,
/// Lanes). A lane's settings, such as its type and speed limit, are those of lane <see cref="NetLaneRecord.Index"/> of
/// its segment's prefab (<see cref="TryGetPrefabLane"/>).
/// </summary>
public sealed class CityNetLanes : ISectionData
{
    private readonly Dictionary<uint, NetLaneRecord> _byId = [];
    private readonly CityNetSegments? _segments;
    private readonly CityNetPrefabs? _prefabs;

    /// <summary>Creates the lanes.</summary>
    /// <param name="lanes">The lanes, ordered by id, each id once, with finite curves.</param>
    /// <param name="segments">The segments, to find each lane's prefab; <see langword="null"/> if the dump has none.</param>
    /// <param name="prefabs">The prefabs; <see langword="null"/> if the dump has none.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityNetLanes(IReadOnlyList<NetLaneRecord> lanes, CityNetSegments? segments, CityNetPrefabs? prefabs)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        foreach (NetLaneRecord lane in lanes)
        {
            if (!_byId.TryAdd(lane.Id, lane))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Lane {lane.Id} is listed twice."), nameof(lanes));
            }
        }

        Lanes = lanes;
        _segments = segments;
        _prefabs = prefabs;
    }

    /// <summary>The lanes, ordered by id.</summary>
    public IReadOnlyList<NetLaneRecord> Lanes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>The nodes bound the area of interest (<see cref="CityNetNodes.AreaOfInterestBounds"/>).</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a lane by its id.</summary>
    /// <param name="id">The lane's id.</param>
    /// <param name="lane">The lane, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out NetLaneRecord? lane) => _byId.TryGetValue(id, out lane);

    /// <summary>Finds a lane's settings: lane <see cref="NetLaneRecord.Index"/> of its segment's prefab.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="settings">The prefab's lane, if the dump has the segment, its prefab and that lane.</param>
    public bool TryGetPrefabLane(NetLaneRecord lane, [NotNullWhen(true)] out NetPrefabLane? settings)
    {
        ArgumentNullException.ThrowIfNull(lane);
        settings = null;
        if (_segments is not null && _segments.TryGet(lane.Segment, out NetSegmentRecord? segment)
            && _prefabs is not null && _prefabs.TryGet(segment.Prefab, out NetPrefabRecord? prefab)
            && lane.Index < prefab.Lanes.Count)
        {
            settings = prefab.Lanes[lane.Index];
        }

        return settings is not null;
    }

    private SectionStatistic[] BuildStatistics()
    {
        double meters = 0;
        int vehicle = 0;
        int pedestrian = 0;
        int parking = 0;
        int other = 0;
        int unknown = 0;
        foreach (NetLaneRecord lane in Lanes)
        {
            meters += lane.Length;
            if (!TryGetPrefabLane(lane, out NetPrefabLane? settings))
            {
                unknown++;
            }
            else if ((settings.LaneType & (NetPrefabLane.VehicleLane | NetPrefabLane.CargoVehicleLane | NetPrefabLane.TransportVehicleLane)) != 0)
            {
                vehicle++;
            }
            else if ((settings.LaneType & NetPrefabLane.PedestrianLane) != 0)
            {
                pedestrian++;
            }
            else if ((settings.LaneType & NetPrefabLane.ParkingLane) != 0)
            {
                parking++;
            }
            else
            {
                other++;
            }
        }

        return
        [
            new("lanes", string.Create(CultureInfo.InvariantCulture, $"{Lanes.Count}, {meters / 1000:0.##} km by the game's lengths")),
            new("by type", string.Create(
                CultureInfo.InvariantCulture,
                $"{vehicle} for vehicles, {pedestrian} for pedestrians, {parking} for parking, {other} other{(unknown == 0 ? string.Empty : $", {unknown} unknown")}")),
        ];
    }
}
