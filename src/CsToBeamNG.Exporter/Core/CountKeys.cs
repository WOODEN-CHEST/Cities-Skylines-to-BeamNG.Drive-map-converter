namespace CsToBeamNG.Exporter.Core;

/// <summary>The keys of the manifest's <c>counts</c> (docs/dump-format.md, <c>counts</c>).</summary>
internal static class CountKeys
{
    /// <summary>Network nodes (<c>NetManager.m_nodes</c>).</summary>
    public const string NetNodes = "netNodes";

    /// <summary>Network segments (<c>NetManager.m_segments</c>).</summary>
    public const string NetSegments = "netSegments";

    /// <summary>Network lanes (<c>NetManager.m_lanes</c>).</summary>
    public const string NetLanes = "netLanes";

    /// <summary>
    /// Network prefabs that nodes and segments use. Only a <c>failureCounts</c> key: prefabs aren't objects in the city,
    /// so <c>counts</c> doesn't have it.
    /// </summary>
    public const string NetPrefabs = "netPrefabs";

    /// <summary>Buildings (<c>BuildingManager.m_buildings</c>), sub-buildings included.</summary>
    public const string Buildings = "buildings";

    /// <summary>Free-standing props (<c>PropManager.m_props</c>), not those that belong to buildings or networks.</summary>
    public const string Props = "props";

    /// <summary>Trees (<c>TreeManager.m_trees</c>).</summary>
    public const string Trees = "trees";
}
