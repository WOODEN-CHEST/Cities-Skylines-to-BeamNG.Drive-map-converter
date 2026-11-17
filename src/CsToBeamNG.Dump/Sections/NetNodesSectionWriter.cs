using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>net-nodes</c> section (docs/dump-format.md, <c>net-nodes</c>).</summary>
public static class NetNodesSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Nodes are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The nodes.</param>
    /// <exception cref="ArgumentException">A node is missing, listed twice, or doesn't have <see cref="NetNodeRecord.SegmentSlots"/> segment slots.</exception>
    public static void Write(DumpWriter dump, NetNodesSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        var nodes = new List<NetNodeRecord>(content.Nodes);
        foreach (NetNodeRecord node in nodes)
        {
            Guard.NotNull(node, nameof(content));
            if (node.Segments.Length != NetNodeRecord.SegmentSlots)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Node {0} has {1} segment slots instead of {2}.", node.Id, node.Segments.Length, NetNodeRecord.SegmentSlots),
                    nameof(content));
            }
        }

        nodes.Sort((a, b) => a.Id.CompareTo(b.Id));
        for (int i = 1; i < nodes.Count; i++)
        {
            if (nodes[i].Id == nodes[i - 1].Id)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Node {0} is listed twice.", nodes[i].Id),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(NetNodesSection.Name, NetNodesSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(NetNodesSection.NodesFileName, NetNodesSection.NodeRecordType, NetNodesSection.NodeRecordSize))
        {
            foreach (NetNodeRecord node in nodes)
            {
                records.BeginRecord();
                records.WriteUInt32(node.Id);
                records.WriteUInt32(node.Flags);
                records.WriteUInt32(node.Flags2);
                records.WriteUInt32(node.Prefab);
                RecordVectors.Write(records, node.Position);
                records.WriteUInt32(node.Building);
                foreach (uint segment in node.Segments)
                {
                    records.WriteUInt32(segment);
                }

                records.WriteByte(node.Elevation);
                records.WriteByte(node.HeightOffset);
                records.WriteZeros(2);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = nodes.Count;
        section.Complete();
    }
}
