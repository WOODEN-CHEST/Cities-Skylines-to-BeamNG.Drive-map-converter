using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>props</c> section (docs/dump-format.md, <c>props</c>).</summary>
public static class PropsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Props are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The props.</param>
    /// <exception cref="ArgumentException">A prop is missing or listed twice.</exception>
    public static void Write(DumpWriter dump, PropsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<PropRecord> props = RecordOrdering.SortUnique(content.Props, prop => prop.Id, "Prop", nameof(content));

        SectionWriter section = dump.BeginSection(PropsSection.Name, PropsSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(PropsSection.PropsFileName, PropsSection.PropRecordType, PropsSection.PropRecordSize))
        {
            foreach (PropRecord prop in props)
            {
                records.BeginRecord();
                records.WriteUInt32(prop.Id);
                records.WriteUInt32(prop.Prefab);
                RecordVectors.Write(records, prop.Position);
                records.WriteSingle(prop.Angle);
                records.WriteSingle(prop.Scale);
                RecordVectors.Write(records, prop.Color);
                records.WriteUInt16(prop.Flags);
                records.WriteZeros(2);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = props.Count;
        section.Complete();
    }
}
