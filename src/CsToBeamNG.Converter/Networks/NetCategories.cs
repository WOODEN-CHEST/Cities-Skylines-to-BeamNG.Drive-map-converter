using System;
using System.Collections.Generic;
using System.Linq;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>Sorts network prefabs into <see cref="NetCategory"/>s (docs/research/cs1-networks.md, Prefabs).</summary>
public static class NetCategories
{
    /// <summary>
    /// The AI type of transport lines, whose segments join the stops of a bus, tram or train line. They follow no
    /// road of their own, so they are never drawn.
    /// </summary>
    public const string TransportLineAiType = "TransportLineAI";

    /// <summary>The item-class service of roads.</summary>
    public const string RoadService = "Road";

    /// <summary>
    /// The item-class levels of small roads: the game's two-lane roads have level 2 (<c>Small Road</c>), medium roads
    /// level 3 and highways level 5 (verified in a dump, docs/research/cs1-networks.md, Prefabs).
    /// </summary>
    public static IReadOnlyList<string> SmallRoadLevels { get; } = ["Level1", "Level2"];

    // The layers of networks that can be seen: on, above or below the ground. Pipes, transport lines and the routes of
    // ships, aircraft and the like have layers of their own.
    private const uint VisibleLayers = NetPrefabRecord.DefaultLayer | NetPrefabRecord.MetroTunnelsLayer;

    // Vehicle lane types that carry traffic, as opposed to parking.
    private const byte TrafficLaneTypes = NetPrefabLane.VehicleLane | NetPrefabLane.CargoVehicleLane | NetPrefabLane.TransportVehicleLane;

    // Vehicles that run on tracks.
    private const uint RailVehicles = NetPrefabLane.MetroVehicles | NetPrefabLane.TrainVehicles | NetPrefabLane.TramVehicles
        | NetPrefabLane.MonorailVehicles | NetPrefabLane.CableCarVehicles;

    /// <summary>
    /// Returns a prefab's category, from its layer, what moves on its lanes and how the game classes it. Only networks
    /// of the visible layers (default and metro tunnels) count, and never transport lines. Among them, a road
    /// (item-class service <c>Road</c>, or lanes for cars) is a <see cref="NetCategory.Highway"/> if highway rules apply,
    /// a <see cref="NetCategory.Local"/> road at the small roads' levels (<see cref="SmallRoadLevels"/>) and an
    /// <see cref="NetCategory.Arterial"/> above; lanes for rail vehicles make a <see cref="NetCategory.Rail"/>, lanes for
    /// pedestrians or bicycles a <see cref="NetCategory.Path"/>. Everything else is <see cref="NetCategory.Other"/>.
    /// </summary>
    /// <param name="prefab">The prefab.</param>
    public static NetCategory Of(NetPrefabRecord prefab)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        if ((prefab.Layer & VisibleLayers) == 0 || string.Equals(prefab.AiType, TransportLineAiType, StringComparison.Ordinal))
        {
            return NetCategory.Other;
        }

        bool cars = false;
        bool rails = false;
        bool people = false;
        foreach (NetPrefabLane lane in prefab.Lanes)
        {
            cars |= (lane.LaneType & TrafficLaneTypes) != 0 && (lane.VehicleType & NetPrefabLane.CarVehicles) != 0;
            rails |= (lane.VehicleType & RailVehicles) != 0;
            people |= (lane.LaneType & NetPrefabLane.PedestrianLane) != 0 || (lane.VehicleType & NetPrefabLane.BicycleVehicles) != 0;
        }

        if (cars || string.Equals(prefab.Service, RoadService, StringComparison.Ordinal))
        {
            return prefab.Highway ? NetCategory.Highway
                : SmallRoadLevels.Contains(prefab.Level, StringComparer.Ordinal) ? NetCategory.Local
                : NetCategory.Arterial;
        }

        return rails ? NetCategory.Rail : people ? NetCategory.Path : NetCategory.Other;
    }
}
