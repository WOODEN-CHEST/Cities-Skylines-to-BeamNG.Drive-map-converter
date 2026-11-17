using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>net-segments</c> section (docs/dump-format.md, <c>net-segments</c>).</summary>
public static class NetSegmentsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Segments are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The segments.</param>
    /// <exception cref="ArgumentException">A segment is missing or listed twice.</exception>
    public static void Write(DumpWriter dump, NetSegmentsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        var segments = new List<NetSegmentRecord>(content.Segments);
        foreach (NetSegmentRecord segment in segments)
        {
            Guard.NotNull(segment, nameof(content));
        }

        segments.Sort((a, b) => a.Id.CompareTo(b.Id));
        for (int i = 1; i < segments.Count; i++)
        {
            if (segments[i].Id == segments[i - 1].Id)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Segment {0} is listed twice.", segments[i].Id),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(NetSegmentsSection.Name, NetSegmentsSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(NetSegmentsSection.SegmentsFileName, NetSegmentsSection.SegmentRecordType, NetSegmentsSection.SegmentRecordSize))
        {
            foreach (NetSegmentRecord segment in segments)
            {
                records.BeginRecord();
                records.WriteUInt32(segment.Id);
                records.WriteUInt32(segment.Flags);
                records.WriteUInt32(segment.Flags2);
                records.WriteUInt32(segment.Prefab);
                records.WriteUInt32(segment.StartNode);
                records.WriteUInt32(segment.EndNode);
                RecordVectors.Write(records, segment.StartDirection);
                RecordVectors.Write(records, segment.EndDirection);
                RecordVectors.Write(records, segment.MiddlePosition);
                RecordVectors.Write(records, segment.Bezier);
                WriteCorner(records, segment.StartLeft);
                WriteCorner(records, segment.StartRight);
                WriteCorner(records, segment.EndLeft);
                WriteCorner(records, segment.EndRight);
                records.WriteSingle(segment.AverageLength);
                records.WriteByte(CornerFlags(segment));
                records.WriteZeros(3);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = segments.Count;
        section.Complete();
    }

    private static void WriteCorner(RecordFileWriter records, NetCorner corner)
    {
        RecordVectors.Write(records, corner.Position);
        RecordVectors.Write(records, corner.Direction);
    }

    private static byte CornerFlags(NetSegmentRecord segment)
    {
        int flags = (segment.StartLeft.Smooth ? NetSegmentsSection.StartLeftSmoothBit : 0)
            | (segment.StartRight.Smooth ? NetSegmentsSection.StartRightSmoothBit : 0)
            | (segment.EndLeft.Smooth ? NetSegmentsSection.EndLeftSmoothBit : 0)
            | (segment.EndRight.Smooth ? NetSegmentsSection.EndRightSmoothBit : 0);
        return (byte)flags;
    }
}
