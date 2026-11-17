using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>net-lanes</c> section (docs/dump-format.md, <c>net-lanes</c>).</summary>
public static class NetLanesSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Lanes are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The lanes.</param>
    /// <exception cref="ArgumentException">A lane is missing or listed twice.</exception>
    public static void Write(DumpWriter dump, NetLanesSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        var lanes = new List<NetLaneRecord>(content.Lanes);
        foreach (NetLaneRecord lane in lanes)
        {
            Guard.NotNull(lane, nameof(content));
        }

        lanes.Sort((a, b) => a.Id.CompareTo(b.Id));
        for (int i = 1; i < lanes.Count; i++)
        {
            if (lanes[i].Id == lanes[i - 1].Id)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Lane {0} is listed twice.", lanes[i].Id),
                    nameof(content));
            }
        }

        SectionWriter section = dump.BeginSection(NetLanesSection.Name, NetLanesSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(NetLanesSection.LanesFileName, NetLanesSection.LaneRecordType, NetLanesSection.LaneRecordSize))
        {
            foreach (NetLaneRecord lane in lanes)
            {
                records.BeginRecord();
                records.WriteUInt32(lane.Id);
                records.WriteUInt32(lane.Segment);
                records.WriteUInt16(lane.Index);
                records.WriteUInt16(lane.Flags);
                RecordVectors.Write(records, lane.Bezier);
                records.WriteSingle(lane.Length);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = lanes.Count;
        section.Complete();
    }
}
