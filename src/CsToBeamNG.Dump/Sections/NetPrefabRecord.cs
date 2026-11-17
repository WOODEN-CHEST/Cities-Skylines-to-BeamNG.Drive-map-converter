using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A network prefab (<c>NetInfo</c>): what kind of network a node or segment is, its dimensions, how it shapes the
/// terrain, its elevation variants and its lanes, as the game stores them (docs/dump-format.md, <c>net-prefabs</c>;
/// docs/research/cs1-networks.md, Prefabs).
/// </summary>
public sealed class NetPrefabRecord
{
    /// <summary><see cref="Layer"/>: the default layer of networks on, above and below the ground (<c>ItemClass.Layer.Default</c>).</summary>
    public const uint DefaultLayer = 0x1;

    /// <summary><see cref="Layer"/>: water pipes (<c>WaterPipes</c>).</summary>
    public const uint WaterPipesLayer = 0x2;

    /// <summary><see cref="Layer"/>: transport lines (<c>PublicTransport</c>).</summary>
    public const uint PublicTransportLayer = 0x4;

    /// <summary><see cref="Layer"/>: metro tunnels (<c>MetroTunnels</c>).</summary>
    public const uint MetroTunnelsLayer = 0x8;

    /// <summary>The prefab's index in the game's prefab collection, which nodes and segments refer to (<c>m_infoIndex</c>).</summary>
    public uint Index { get; set; }

    /// <summary>The prefab's name, such as <c>Basic Road</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The name of the prefab's item class (<c>m_class</c>), such as <c>Small Road</c>.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>The item class's service, as the game names it (<c>ItemClass.Service</c>), such as <c>Road</c>.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>The item class's sub-service, as the game names it (<c>ItemClass.SubService</c>), such as <c>PublicTransportTrain</c>.</summary>
    public string SubService { get; set; } = string.Empty;

    /// <summary>The item class's level, as the game names it (<c>ItemClass.Level</c>), such as <c>Level1</c>.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>The item class's layers, the raw value of <c>ItemClass.Layer</c>, a set of bits.</summary>
    public uint Layer { get; set; }

    /// <summary>The full type name of the prefab's AI (<c>m_netAI</c>), such as <c>RoadAI</c>; empty if it has none.</summary>
    public string AiType { get; set; } = string.Empty;

    /// <summary>What the AI's <c>IsHighway()</c> returns: whether highway rules apply.</summary>
    public bool Highway { get; set; }

    /// <summary>What the AI's <c>IsBridge()</c> returns.</summary>
    public bool Bridge { get; set; }

    /// <summary>What the AI's <c>IsTunnel()</c> returns.</summary>
    public bool Tunnel { get; set; }

    /// <summary>What the AI's <c>IsUnderground()</c> returns: whether the network runs underground.</summary>
    public bool Underground { get; set; }

    /// <summary>What the AI's <c>IsOverground()</c> returns: whether the network runs above the ground, such as a bridge.</summary>
    public bool Overground { get; set; }

    /// <summary>Half the network's width, in metres (<c>m_halfWidth</c>).</summary>
    public float HalfWidth { get; set; }

    /// <summary>The width of the pavement on each side, in metres (<c>m_pavementWidth</c>).</summary>
    public float PavementWidth { get; set; }

    /// <summary>The length of the prefab's segment mesh, in metres (<c>m_segmentLength</c>).</summary>
    public float SegmentLength { get; set; }

    /// <summary>The height of the driving surface relative to the nodes, in metres (<c>m_surfaceLevel</c>).</summary>
    public float SurfaceLevel { get; set; }

    /// <summary>Whether the network paints pavement onto the terrain (<c>m_createPavement</c>).</summary>
    public bool CreatePavement { get; set; }

    /// <summary>Whether the network paints gravel onto the terrain (<c>m_createGravel</c>).</summary>
    public bool CreateGravel { get; set; }

    /// <summary>Whether the network paints ruined ground onto the terrain (<c>m_createRuining</c>).</summary>
    public bool CreateRuining { get; set; }

    /// <summary>Whether the network levels the terrain under it (<c>m_flattenTerrain</c>).</summary>
    public bool FlattenTerrain { get; set; }

    /// <summary>Whether the network lowers the terrain under it (<c>m_lowerTerrain</c>).</summary>
    public bool LowerTerrain { get; set; }

    /// <summary>Whether the network cuts the terrain where it would cover it (<c>m_clipTerrain</c>).</summary>
    public bool ClipTerrain { get; set; }

    /// <summary>Whether the network follows the terrain instead of levelling it (<c>m_followTerrain</c>).</summary>
    public bool FollowTerrain { get; set; }

    /// <summary>Whether the network's junctions are level (<c>m_flatJunctions</c>).</summary>
    public bool FlatJunctions { get; set; }

    /// <summary>The name of the elevated version, from the AI's <c>m_elevatedInfo</c>; <see langword="null"/> for none.</summary>
    public string? ElevatedVariant { get; set; }

    /// <summary>The name of the bridge version, from the AI's <c>m_bridgeInfo</c>; <see langword="null"/> for none.</summary>
    public string? BridgeVariant { get; set; }

    /// <summary>The name of the slope version, from the AI's <c>m_slopeInfo</c>; <see langword="null"/> for none.</summary>
    public string? SlopeVariant { get; set; }

    /// <summary>The name of the tunnel version, from the AI's <c>m_tunnelInfo</c>; <see langword="null"/> for none.</summary>
    public string? TunnelVariant { get; set; }

    /// <summary>The prefab's lanes, in the game's order, which a lane's <see cref="NetLaneRecord.Index"/> refers to.</summary>
    public IList<NetPrefabLane> Lanes { get; } = new List<NetPrefabLane>();
}
