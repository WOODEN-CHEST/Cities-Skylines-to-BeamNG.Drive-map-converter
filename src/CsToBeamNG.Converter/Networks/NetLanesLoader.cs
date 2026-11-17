using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Networks;

/// <summary>Loads the <c>net-lanes</c> section into a <see cref="CityNetLanes"/> (docs/dump-format.md, <c>net-lanes</c>).</summary>
public sealed class NetLanesLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => NetLanesSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => NetLanesSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A lane whose curve or length isn't finite, or whose id was listed already, is skipped with a warning. Lanes whose
    /// segment the <c>net-segments</c> section lacks, or whose index its prefab doesn't have, are kept and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        NetLanesSectionContent content = NetLanesSectionReader.Read(section);
        CityNetSegments? segments = context.Find<CityNetSegments>();
        CityNetPrefabs? prefabs = context.Find<CityNetPrefabs>();
        var unknownSegments = new MissingReferences("lanes belong to segments that the net-segments section lacks");
        var unknownIndices = new MissingReferences("lanes have an index beyond their prefab's lanes");
        var lanes = new List<NetLaneRecord>(content.Lanes.Count);
        var ids = new HashSet<uint>();
        foreach (NetLaneRecord lane in content.Lanes.OrderBy(lane => lane.Id))
        {
            if (!lane.Bezier.IsFinite || !float.IsFinite(lane.Length))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Lane {lane.Id} has a curve or length that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(lane.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Lane {lane.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (segments is not null)
            {
                if (!segments.TryGet(lane.Segment, out NetSegmentRecord? segment))
                {
                    unknownSegments.Add(string.Create(CultureInfo.InvariantCulture, $"lane {lane.Id} (segment {lane.Segment})"));
                }
                else if (prefabs is not null && prefabs.TryGet(segment.Prefab, out NetPrefabRecord? prefab) && lane.Index >= prefab.Lanes.Count)
                {
                    unknownIndices.Add(string.Create(CultureInfo.InvariantCulture, $"lane {lane.Id} (index {lane.Index} of {prefab.Lanes.Count} in \"{prefab.Name}\")"));
                }
            }

            lanes.Add(lane);
        }

        unknownSegments.Report(context);
        unknownIndices.Report(context);
        return new CityNetLanes(lanes, segments, prefabs);
    }
}
