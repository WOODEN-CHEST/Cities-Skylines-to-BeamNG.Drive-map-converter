using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>buildings</c> section (docs/dump-format.md, <c>buildings</c>).</summary>
public static class BuildingsSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Buildings are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The buildings.</param>
    /// <exception cref="ArgumentException">A building is missing or listed twice.</exception>
    public static void Write(DumpWriter dump, BuildingsSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<BuildingRecord> buildings = RecordOrdering.SortUnique(content.Buildings, building => building.Id, "Building", nameof(content));

        SectionWriter section = dump.BeginSection(BuildingsSection.Name, BuildingsSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(BuildingsSection.BuildingsFileName, BuildingsSection.BuildingRecordType, BuildingsSection.BuildingRecordSize))
        {
            foreach (BuildingRecord building in buildings)
            {
                records.BeginRecord();
                records.WriteUInt32(building.Id);
                records.WriteUInt32(building.Flags);
                records.WriteUInt32(building.Flags2);
                records.WriteUInt32(building.Prefab);
                RecordVectors.Write(records, building.Position);
                records.WriteSingle(building.Angle);
                RecordVectors.Write(records, building.MeshPosition);
                records.WriteUInt32(building.ParentBuilding);
                records.WriteUInt32(building.SubBuilding);
                RecordVectors.Write(records, building.Color);
                records.WriteByte(building.Width);
                records.WriteByte(building.Length);
                records.WriteByte(building.Level);
                records.WriteByte(building.BaseHeight);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = buildings.Count;
        section.Complete();
    }
}
