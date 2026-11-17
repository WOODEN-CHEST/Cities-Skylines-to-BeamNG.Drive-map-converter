using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// The city's network nodes, by id, in the game's coordinates (docs/dump-format.md, <c>net-nodes</c>;
/// docs/research/cs1-networks.md, Data model). They bound the area of interest with the nodes inside the unlocked tiles.
/// </summary>
public sealed class CityNetNodes : ISectionData
{
    private readonly Dictionary<uint, NetNodeRecord> _byId = [];

    /// <summary>Creates the nodes.</summary>
    /// <param name="nodes">The nodes, ordered by id, each id once, with finite positions.</param>
    /// <param name="unlockedTiles">The city's unlocked tiles, which bound where its own network can be; empty if unknown.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityNetNodes(IReadOnlyList<NetNodeRecord> nodes, IReadOnlyList<TileCoordinate> unlockedTiles)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(unlockedTiles);
        foreach (NetNodeRecord node in nodes)
        {
            if (!_byId.TryAdd(node.Id, node))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Node {node.Id} is listed twice."), nameof(nodes));
            }
        }

        Nodes = nodes;
        (AreaOfInterestBounds, ContentNodes) = Bounds(nodes, unlockedTiles);
    }

    /// <summary>The nodes, ordered by id.</summary>
    public IReadOnlyList<NetNodeRecord> Nodes { get; }

    /// <summary>
    /// The number of nodes that <see cref="AreaOfInterestBounds"/> encloses: those inside the unlocked tiles, or all of
    /// them if the unlocked tiles aren't known.
    /// </summary>
    public int ContentNodes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>
    /// The bounds of the nodes inside the unlocked tiles: the city can only build there, while the map's own
    /// highways, tracks and ship and air routes run to the map's edges (docs/architecture.md, D32). Without unlocked
    /// tiles, the bounds of all nodes; <see langword="null"/> without nodes.
    /// </remarks>
    public MapRect? AreaOfInterestBounds { get; }

    /// <summary>Looks up a node by its id.</summary>
    /// <param name="id">The node's id.</param>
    /// <param name="node">The node, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out NetNodeRecord? node) => _byId.TryGetValue(id, out node);

    private static (MapRect? Bounds, int Count) Bounds(IReadOnlyList<NetNodeRecord> nodes, IReadOnlyList<TileCoordinate> unlockedTiles)
    {
        var tiles = new List<MapRect>(unlockedTiles.Count);
        foreach (TileCoordinate tile in unlockedTiles)
        {
            tiles.Add(MapGeometry.TileBounds(tile));
        }

        float minX = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;
        int count = 0;
        foreach (NetNodeRecord node in nodes)
        {
            float x = node.Position.X;
            float z = node.Position.Z;
            if (tiles.Count > 0 && !tiles.Exists(tile => x >= tile.MinX && x <= tile.MaxX && z >= tile.MinZ && z <= tile.MaxZ))
            {
                continue;
            }

            minX = Math.Min(minX, x);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxZ = Math.Max(maxZ, z);
            count++;
        }

        return count == 0 ? (null, 0) : (new MapRect(minX, minZ, maxX, maxZ), count);
    }

    private SectionStatistic[] BuildStatistics()
    {
        int junctions = 0;
        int bends = 0;
        int ends = 0;
        int middles = 0;
        int trafficLights = 0;
        int outside = 0;
        int underground = 0;
        int elevated = 0;
        foreach (NetNodeRecord node in Nodes)
        {
            junctions += node.HasFlags(NetNodeRecord.JunctionFlag) ? 1 : 0;
            bends += node.HasFlags(NetNodeRecord.BendFlag) ? 1 : 0;
            ends += node.HasFlags(NetNodeRecord.EndFlag) ? 1 : 0;
            middles += node.HasFlags(NetNodeRecord.MiddleFlag) ? 1 : 0;
            trafficLights += node.HasFlags(NetNodeRecord.TrafficLightsFlag) ? 1 : 0;
            outside += node.HasFlags(NetNodeRecord.OutsideFlag) ? 1 : 0;
            bool isUnderground = node.HasFlags(NetNodeRecord.UndergroundFlag);
            underground += isUnderground ? 1 : 0;
            elevated += !isUnderground && node.Elevation > 0 ? 1 : 0;
        }

        return
        [
            new("nodes", string.Create(
                CultureInfo.InvariantCulture,
                $"{Nodes.Count}: {junctions} junctions, {bends} bends, {ends} ends, {middles} middle nodes")),
            new("traffic lights", string.Create(CultureInfo.InvariantCulture, $"at {trafficLights} nodes")),
            new("outside connections", outside.ToString(CultureInfo.InvariantCulture)),
            new("off the ground", string.Create(CultureInfo.InvariantCulture, $"{elevated} elevated, {underground} underground")),
            new("content", AreaOfInterestBounds is { } bounds
                ? string.Create(CultureInfo.InvariantCulture, $"{ContentNodes} nodes within {bounds}")
                : "none"),
        ];
    }
}
