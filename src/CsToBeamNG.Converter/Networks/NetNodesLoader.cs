using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>Loads the <c>net-nodes</c> section into a <see cref="CityNetNodes"/> (docs/dump-format.md, <c>net-nodes</c>).</summary>
public sealed class NetNodesLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => NetNodesSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => NetNodesSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A node whose position isn't finite, or whose id was listed already, is skipped with a warning. Nodes whose prefab
    /// the <c>net-prefabs</c> section lacks are kept and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        NetNodesSectionContent content = NetNodesSectionReader.Read(section);
        CityNetPrefabs? prefabs = context.Find<CityNetPrefabs>();
        var unknownPrefabs = new MissingReferences("nodes have a prefab that the net-prefabs section lacks");
        var nodes = new List<NetNodeRecord>(content.Nodes.Count);
        var ids = new HashSet<uint>();
        foreach (NetNodeRecord node in content.Nodes.OrderBy(node => node.Id))
        {
            if (!node.Position.IsFinite)
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Node {node.Id} has a position that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(node.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Node {node.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (prefabs is not null && !prefabs.TryGet(node.Prefab, out _))
            {
                unknownPrefabs.Add(string.Create(CultureInfo.InvariantCulture, $"node {node.Id} (prefab {node.Prefab})"));
            }

            nodes.Add(node);
        }

        unknownPrefabs.Report(context);
        return new CityNetNodes(nodes, [.. context.Manifest.City.UnlockedTiles]);
    }
}
