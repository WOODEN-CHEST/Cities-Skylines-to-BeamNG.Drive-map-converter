using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>trees</c> section (docs/dump-format.md, <c>trees</c>).</summary>
public static class TreesSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Trees are written ordered by id,
    /// whatever their order in <paramref name="content"/>; the section's record count is their number.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The trees.</param>
    /// <exception cref="ArgumentException">A tree is missing or listed twice.</exception>
    public static void Write(DumpWriter dump, TreesSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        Guard.NotNull(content, nameof(content));
        List<TreeRecord> trees = RecordOrdering.SortUnique(content.Trees, tree => tree.Id, "Tree", nameof(content));

        SectionWriter section = dump.BeginSection(TreesSection.Name, TreesSection.Version);
        using (RecordFileWriter records = section.CreateRecordFile(TreesSection.TreesFileName, TreesSection.TreeRecordType, TreesSection.TreeRecordSize))
        {
            foreach (TreeRecord tree in trees)
            {
                records.BeginRecord();
                records.WriteUInt32(tree.Id);
                records.WriteUInt32(tree.Prefab);
                RecordVectors.Write(records, tree.Position);
                records.WriteSingle(tree.Scale);
                records.WriteSingle(tree.Brightness);
                records.WriteUInt16(tree.Flags);
                records.WriteZeros(2);
                records.EndRecord();
            }

            records.Finish();
        }

        section.RecordCount = trees.Count;
        section.Complete();
    }
}
