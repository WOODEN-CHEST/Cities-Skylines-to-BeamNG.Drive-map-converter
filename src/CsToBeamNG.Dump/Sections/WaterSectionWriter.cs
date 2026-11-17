using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>water</c> section (docs/dump-format.md, <c>water</c>).</summary>
public static class WaterSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Sources are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The water.</param>
    /// <exception cref="ArgumentException">The content is inconsistent, such as an array of the wrong length.</exception>
    public static void Write(DumpWriter dump, WaterSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        List<WaterSourceRecord> sources = Validate(content);
        SectionWriter section = dump.BeginSection(WaterSection.Name, WaterSection.Version);
        using (JsonWriter json = section.CreateJsonFile(WaterSection.InfoFileName))
        {
            json.WriteStartObject();
            json.WriteNumber("seaLevel", content.SeaLevel);
            content.Grid.Write(json, "grid");
            json.WriteEndObject();
        }

        using (RecordFileWriter cells = section.CreateRecordFile(WaterSection.CellsFileName, WaterSection.CellRecordType, WaterSection.CellRecordSize))
        {
            for (int i = 0; i < content.Depth.Length; i++)
            {
                cells.BeginRecord();
                cells.WriteUInt16(content.Ground[i]);
                cells.WriteUInt16(content.Depth[i]);
                cells.WriteUInt16(content.Pollution[i]);
                cells.WriteInt16(content.VelocityX[i]);
                cells.WriteInt16(content.VelocityZ[i]);
                cells.EndRecord();
            }

            cells.Finish();
        }

        using (RecordFileWriter records = section.CreateRecordFile(WaterSection.SourcesFileName, WaterSection.SourceRecordType, WaterSection.SourceRecordSize))
        {
            foreach (WaterSourceRecord source in sources)
            {
                records.BeginRecord();
                records.WriteUInt32(source.Id);
                records.WriteUInt16(source.Type);
                records.WriteUInt16(source.Target);
                records.WriteSingle(source.InputX);
                records.WriteSingle(source.InputY);
                records.WriteSingle(source.InputZ);
                records.WriteSingle(source.OutputX);
                records.WriteSingle(source.OutputY);
                records.WriteSingle(source.OutputZ);
                records.WriteUInt32(source.Water);
                records.WriteUInt32(source.Pollution);
                records.WriteUInt32(source.InputRate);
                records.WriteUInt32(source.OutputRate);
                records.WriteUInt32(source.Flow);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = sources.Count;
        section.Complete();
    }

    private static List<WaterSourceRecord> Validate(WaterSectionContent content)
    {
        Guard.NotNull(content, nameof(content));
        if (float.IsNaN(content.SeaLevel) || float.IsInfinity(content.SeaLevel))
        {
            throw new ArgumentException("The sea level must be finite.", nameof(content));
        }

        int points = content.Grid.PointCount;
        CheckLength("ground heights", content.Ground.Length, points);
        CheckLength("depths", content.Depth.Length, points);
        CheckLength("pollution values", content.Pollution.Length, points);
        CheckLength("X velocities", content.VelocityX.Length, points);
        CheckLength("Z velocities", content.VelocityZ.Length, points);
        var sources = new List<WaterSourceRecord>(content.Sources);
        foreach (WaterSourceRecord source in sources)
        {
            Guard.NotNull(source, nameof(content));
        }

        sources.Sort((a, b) => a.Id.CompareTo(b.Id));
        for (int i = 1; i < sources.Count; i++)
        {
            if (sources[i].Id == sources[i - 1].Id)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Water source {0} is listed twice.", sources[i].Id),
                    nameof(content));
            }
        }

        return sources;
    }

    private static void CheckLength(string what, int length, int expected)
    {
        if (length != expected)
        {
            throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                "There are {0} {1}, but the grid has {2} points.",
                length,
                what,
                expected));
        }
    }
}
