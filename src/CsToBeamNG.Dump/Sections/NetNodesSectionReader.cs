#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>net-nodes</c> section written by <see cref="NetNodesSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class NetNodesSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="NetNodesSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static NetNodesSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new NetNodesSectionContent();
        using RecordFileReader records = section.OpenRecordFile(NetNodesSection.NodesFileName, NetNodesSection.NodeRecordType, NetNodesSection.NodeRecordSize);
        while (records.MoveNext())
        {
            var node = new NetNodeRecord
            {
                Id = records.ReadUInt32(),
                Flags = records.ReadUInt32(),
                Flags2 = records.ReadUInt32(),
                Prefab = records.ReadUInt32(),
                Position = RecordVectors.ReadVector(records),
                Building = records.ReadUInt32(),
            };
            for (int i = 0; i < NetNodeRecord.SegmentSlots; i++)
            {
                node.Segments[i] = records.ReadUInt32();
            }

            node.Elevation = records.ReadByte();
            node.HeightOffset = records.ReadByte();
            content.Nodes.Add(node);
        }

        return content;
    }
}
#endif
