using System;
using ColossalFramework;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the city from the game's managers into a <see cref="CitySnapshot"/>, without changing anything.
/// Create it on the main thread; call <see cref="Read"/> on the simulation thread.
/// </summary>
internal sealed class CityReader
{
    private readonly SimulationManager _simulation;
    private readonly NetManager _net;
    private readonly BuildingManager _buildings;
    private readonly PropManager _props;
    private readonly TreeManager _trees;
    private readonly GameAreaManager _areas;

    private CityReader(
        SimulationManager simulation,
        NetManager net,
        BuildingManager buildings,
        PropManager props,
        TreeManager trees,
        GameAreaManager areas)
    {
        _simulation = simulation;
        _net = net;
        _buildings = buildings;
        _props = props;
        _trees = trees;
        _areas = areas;
    }

    /// <summary>
    /// Finds the game's managers. Main thread only, because <c>Singleton&lt;T&gt;.instance</c> creates a
    /// missing manager through Unity (docs/research/cs1-save-and-world.md, Threads). It checks first and
    /// fails instead, because the exporter must not change the game.
    /// </summary>
    /// <exception cref="InvalidOperationException">A manager doesn't exist.</exception>
    public static CityReader Create() => new(
        Require<SimulationManager>(),
        Require<NetManager>(),
        Require<BuildingManager>(),
        Require<PropManager>(),
        Require<TreeManager>(),
        Require<GameAreaManager>());

    /// <summary>
    /// Returns the city's name for the dump folder's name, before the snapshot is read. Reading one string
    /// reference outside the simulation thread is harmless; the manifest gets the name from the snapshot.
    /// </summary>
    public string ReadCityName() => _simulation.m_metaData?.m_CityName ?? string.Empty;

    /// <summary>
    /// Reads the snapshot. Call it on the simulation thread, through <c>SimulationManager.AddAction</c>, so
    /// that the simulation can't change the data while it is read (docs/research/cs1-save-and-world.md, Threads).
    /// </summary>
    public CitySnapshot Read()
    {
        var snapshot = new CitySnapshot();
        SimulationMetaData? metaData = _simulation.m_metaData;
        if (metaData is not null)
        {
            snapshot.CityName = metaData.m_CityName ?? string.Empty;
            snapshot.MapName = metaData.m_MapName;
            snapshot.Environment = metaData.m_environment;
            snapshot.MapTheme = metaData.m_MapThemeMetaData?.name;
            snapshot.LeftHandTraffic = metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
        }

        snapshot.AreaGrid = (int[]?)_areas.m_areaGrid?.Clone();
        snapshot.Counts.Add(CountNodes());
        snapshot.Counts.Add(CountSegments());
        snapshot.Counts.Add(CountLanes());
        snapshot.Counts.Add(CountBuildings());
        snapshot.Counts.Add(CountProps());
        snapshot.Counts.Add(CountTrees());
        return snapshot;
    }

    /// <summary>
    /// Returns a manager of the game without creating it. Main thread only (see <see cref="Create"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    internal static T Require<T>()
        where T : Component
    {
        if (!Singleton<T>.exists)
        {
            throw new InvalidOperationException("The game's " + typeof(T).Name + " doesn't exist. Is a city loaded?");
        }

        return Singleton<T>.instance;
    }

    // Each count is the number of buffer entries whose Created flag is set. The buffers' lengths are the
    // instance limits, which mods may raise by replacing the buffers, so the loops use the actual length.
    private ObjectCount CountNodes()
    {
        NetNode[] buffer = _net.m_nodes.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if ((buffer[i].m_flags & NetNode.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.NetNodes, created, _net.m_nodeCount, buffer.Length);
    }

    private ObjectCount CountSegments()
    {
        NetSegment[] buffer = _net.m_segments.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if ((buffer[i].m_flags & NetSegment.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.NetSegments, created, _net.m_segmentCount, buffer.Length);
    }

    private ObjectCount CountLanes()
    {
        NetLane[] buffer = _net.m_lanes.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (((NetLane.Flags)buffer[i].m_flags & NetLane.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.NetLanes, created, _net.m_laneCount, buffer.Length);
    }

    private ObjectCount CountBuildings()
    {
        Building[] buffer = _buildings.m_buildings.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if ((buffer[i].m_flags & Building.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.Buildings, created, _buildings.m_buildingCount, buffer.Length);
    }

    private ObjectCount CountProps()
    {
        PropInstance[] buffer = _props.m_props.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (((PropInstance.Flags)buffer[i].m_flags & PropInstance.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.Props, created, _props.m_propCount, buffer.Length);
    }

    private ObjectCount CountTrees()
    {
        TreeInstance[] buffer = _trees.m_trees.m_buffer;
        long created = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (((TreeInstance.Flags)buffer[i].m_flags & TreeInstance.Flags.Created) != 0)
            {
                created++;
            }
        }

        return new ObjectCount(CountKeys.Trees, created, _trees.m_treeCount, buffer.Length);
    }
}
