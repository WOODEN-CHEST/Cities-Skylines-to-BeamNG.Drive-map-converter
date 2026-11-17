#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>buildings</c> section written by <see cref="BuildingsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class BuildingsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="BuildingsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static BuildingsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new BuildingsSectionContent();
        using RecordFileReader records = section.OpenRecordFile(BuildingsSection.BuildingsFileName, BuildingsSection.BuildingRecordType, BuildingsSection.BuildingRecordSize);
        while (records.MoveNext())
        {
            content.Buildings.Add(new BuildingRecord
            {
                Id = records.ReadUInt32(),
                Flags = records.ReadUInt32(),
                Flags2 = records.ReadUInt32(),
                Prefab = records.ReadUInt32(),
                Position = RecordVectors.ReadVector(records),
                Angle = records.ReadSingle(),
                MeshPosition = RecordVectors.ReadVector(records),
                ParentBuilding = records.ReadUInt32(),
                SubBuilding = records.ReadUInt32(),
                Color = RecordVectors.ReadColor(records),
                Width = records.ReadByte(),
                Length = records.ReadByte(),
                Level = records.ReadByte(),
                BaseHeight = records.ReadByte(),
            });
        }

        return content;
    }
}
#endif
