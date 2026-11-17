#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>net-lanes</c> section written by <see cref="NetLanesSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class NetLanesSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="NetLanesSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static NetLanesSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new NetLanesSectionContent();
        using RecordFileReader records = section.OpenRecordFile(NetLanesSection.LanesFileName, NetLanesSection.LaneRecordType, NetLanesSection.LaneRecordSize);
        while (records.MoveNext())
        {
            content.Lanes.Add(new NetLaneRecord
            {
                Id = records.ReadUInt32(),
                Segment = records.ReadUInt32(),
                Index = records.ReadUInt16(),
                Flags = records.ReadUInt16(),
                Bezier = RecordVectors.ReadBezier(records),
                Length = records.ReadSingle(),
            });
        }

        return content;
    }
}
#endif
