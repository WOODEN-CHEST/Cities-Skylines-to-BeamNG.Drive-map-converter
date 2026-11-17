#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>props</c> section written by <see cref="PropsSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class PropsSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="PropsSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static PropsSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new PropsSectionContent();
        using RecordFileReader records = section.OpenRecordFile(PropsSection.PropsFileName, PropsSection.PropRecordType, PropsSection.PropRecordSize);
        while (records.MoveNext())
        {
            content.Props.Add(new PropRecord
            {
                Id = records.ReadUInt32(),
                Prefab = records.ReadUInt32(),
                Position = RecordVectors.ReadVector(records),
                Angle = records.ReadSingle(),
                Scale = records.ReadSingle(),
                Color = RecordVectors.ReadColor(records),
                Flags = records.ReadUInt16(),
            });
        }

        return content;
    }
}
#endif
