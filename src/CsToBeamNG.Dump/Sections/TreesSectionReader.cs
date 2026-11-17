#if NET
using System;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>trees</c> section written by <see cref="TreesSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class TreesSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="TreesSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">The file is missing or damaged.</exception>
    public static TreesSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var content = new TreesSectionContent();
        using RecordFileReader records = section.OpenRecordFile(TreesSection.TreesFileName, TreesSection.TreeRecordType, TreesSection.TreeRecordSize);
        while (records.MoveNext())
        {
            content.Trees.Add(new TreeRecord
            {
                Id = records.ReadUInt32(),
                Prefab = records.ReadUInt32(),
                Position = RecordVectors.ReadVector(records),
                Scale = records.ReadSingle(),
                Brightness = records.ReadSingle(),
                Flags = records.ReadUInt16(),
            });
        }

        return content;
    }
}
#endif
