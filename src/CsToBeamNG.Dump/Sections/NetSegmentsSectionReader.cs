#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>net-segments</c> section written by <see cref="NetSegmentsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class NetSegmentsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="NetSegmentsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static NetSegmentsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new NetSegmentsSectionContent();
        using RecordFileReader records = section.OpenRecordFile(NetSegmentsSection.SegmentsFileName, NetSegmentsSection.SegmentRecordType, NetSegmentsSection.SegmentRecordSize);
        while (records.MoveNext())
        {
            var segment = new NetSegmentRecord
            {
                Id = records.ReadUInt32(),
                Flags = records.ReadUInt32(),
                Flags2 = records.ReadUInt32(),
                Prefab = records.ReadUInt32(),
                StartNode = records.ReadUInt32(),
                EndNode = records.ReadUInt32(),
                StartDirection = RecordVectors.ReadVector(records),
                EndDirection = RecordVectors.ReadVector(records),
                MiddlePosition = RecordVectors.ReadVector(records),
                Bezier = RecordVectors.ReadBezier(records),
            };

            // The corners' positions and directions come first; their smoothness is in the flags byte after the length.
            DumpVector3 startLeft = RecordVectors.ReadVector(records);
            DumpVector3 startLeftDirection = RecordVectors.ReadVector(records);
            DumpVector3 startRight = RecordVectors.ReadVector(records);
            DumpVector3 startRightDirection = RecordVectors.ReadVector(records);
            DumpVector3 endLeft = RecordVectors.ReadVector(records);
            DumpVector3 endLeftDirection = RecordVectors.ReadVector(records);
            DumpVector3 endRight = RecordVectors.ReadVector(records);
            DumpVector3 endRightDirection = RecordVectors.ReadVector(records);
            segment.AverageLength = records.ReadSingle();
            byte cornerFlags = records.ReadByte();
            segment.StartLeft = new NetCorner(startLeft, startLeftDirection, (cornerFlags & NetSegmentsSection.StartLeftSmoothBit) != 0);
            segment.StartRight = new NetCorner(startRight, startRightDirection, (cornerFlags & NetSegmentsSection.StartRightSmoothBit) != 0);
            segment.EndLeft = new NetCorner(endLeft, endLeftDirection, (cornerFlags & NetSegmentsSection.EndLeftSmoothBit) != 0);
            segment.EndRight = new NetCorner(endRight, endRightDirection, (cornerFlags & NetSegmentsSection.EndRightSmoothBit) != 0);
            content.Segments.Add(segment);
        }

        return content;
    }
}
#endif
