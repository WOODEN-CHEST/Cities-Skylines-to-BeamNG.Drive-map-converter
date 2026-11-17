using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>Loads the <c>net-segments</c> section into a <see cref="CityNetSegments"/> (docs/dump-format.md, <c>net-segments</c>).</summary>
public sealed class NetSegmentsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => NetSegmentsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => NetSegmentsSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A segment with a value that isn't finite, or whose id was listed already, is skipped with a warning. Segments
    /// whose nodes or prefab the other network sections lack are kept and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        NetSegmentsSectionContent content = NetSegmentsSectionReader.Read(section);
        CityNetPrefabs? prefabs = context.Find<CityNetPrefabs>();
        CityNetNodes? nodes = context.Find<CityNetNodes>();
        var unknownPrefabs = new MissingReferences("segments have a prefab that the net-prefabs section lacks");
        var unknownNodes = new MissingReferences("segments end at nodes that the net-nodes section lacks");
        var segments = new List<NetSegmentRecord>(content.Segments.Count);
        var ids = new HashSet<uint>();
        foreach (NetSegmentRecord segment in content.Segments.OrderBy(segment => segment.Id))
        {
            if (!IsFinite(segment))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Segment {segment.Id} has a position, direction or length that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(segment.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Segment {segment.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (prefabs is not null && !prefabs.TryGet(segment.Prefab, out _))
            {
                unknownPrefabs.Add(string.Create(CultureInfo.InvariantCulture, $"segment {segment.Id} (prefab {segment.Prefab})"));
            }

            if (nodes is not null && (!nodes.TryGet(segment.StartNode, out _) || !nodes.TryGet(segment.EndNode, out _)))
            {
                unknownNodes.Add(string.Create(CultureInfo.InvariantCulture, $"segment {segment.Id} (nodes {segment.StartNode} and {segment.EndNode})"));
            }

            segments.Add(segment);
        }

        unknownPrefabs.Report(context);
        unknownNodes.Report(context);
        return new CityNetSegments(segments, prefabs);
    }

    private static bool IsFinite(NetSegmentRecord segment) =>
        segment.Bezier.IsFinite && segment.StartDirection.IsFinite && segment.EndDirection.IsFinite && segment.MiddlePosition.IsFinite
        && segment.StartLeft.IsFinite && segment.StartRight.IsFinite && segment.EndLeft.IsFinite && segment.EndRight.IsFinite
        && float.IsFinite(segment.AverageLength);
}
