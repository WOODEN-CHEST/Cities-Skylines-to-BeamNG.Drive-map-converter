using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpWriterTests
{
    [Fact]
    public void WritesADumpThatTheReaderOpens()
    {
        using var folder = new TempFolder();
        string dumpFolder = folder.PathOf("seaside_20261117-093000");
        DumpManifest manifest = TestManifests.Minimal();
        manifest.Complete = false;
        manifest.Counts["netNodes"] = 3;
        DumpWriter dump = DumpWriter.Create(dumpFolder, manifest);

        // Written early, so that an interrupted export leaves a manifest that says so.
        dump.WriteManifest();

        SectionWriter nodes = dump.BeginSection("net-nodes", new DumpVersion(0, 1));
        var names = new StringTableBuilder();
        using (RecordFileWriter records = nodes.CreateRecordFile("nodes.bin", "netNode", 8))
        {
            foreach ((uint id, string name) in (List<(uint, string)>)[(1, "Basic Road"), (2, "Oak"), (3, "Basic Road")])
            {
                records.BeginRecord();
                records.WriteUInt32(id);
                records.WriteUInt32(names.Add(name));
                records.EndRecord();
            }

            records.Finish();
        }

        nodes.WriteStringTable("strings.json", names);
        nodes.RecordCount = 3;
        nodes.Complete();

        SectionWriter terrain = dump.BeginSection("terrain", new DumpVersion(1, 0));
        using (RecordFileWriter heights = terrain.CreateRecordFile("heights/raw.bin", "height", 2))
        {
            foreach (ushort height in (ushort[])[0, 64, 65535, 1024])
            {
                heights.BeginRecord();
                heights.WriteUInt16(height);
                heights.EndRecord();
            }

            heights.Finish();
        }

        using (JsonWriter grid = terrain.CreateJsonFile("grid.json"))
        {
            grid.WriteStartObject();
            grid.WriteNumber("spacingMeters", 16f);
            grid.WriteNumber("size", 1081);
            grid.WriteEndObject();
        }

        terrain.Complete();
        dump.Manifest.Complete = true;
        dump.WriteManifest();

        DumpReader reader = DumpReader.Open(dumpFolder);
        Assert.Equal(Path.GetFullPath(dumpFolder), reader.Folder);
        Assert.Empty(reader.Notices);
        Assert.True(reader.Manifest.Complete);
        Assert.Equal(3, reader.Manifest.Counts["netNodes"]);
        Assert.Equal(["net-nodes", "terrain"], reader.Sections.Select(section => section.Name));
        Assert.False(File.Exists(Path.Join(dumpFolder, "manifest.json.tmp")));
        Assert.Null(reader.FindSection("trees"));

        DumpSection? nodeSection = reader.FindSection("net-nodes");
        Assert.NotNull(nodeSection);
        Assert.Equal(new DumpVersion(0, 1), nodeSection.Version);
        Assert.Equal(3, nodeSection.RecordCount);
        Assert.Equal(["nodes.bin", "strings.json"], nodeSection.Files.Select(file => file.Path));
        Assert.Equal(64 + (3 * 8), nodeSection.Files[0].Bytes);
        StringTable strings = nodeSection.ReadStringTable("strings.json");
        using (RecordFileReader records = nodeSection.OpenRecordFile("nodes.bin", "netNode", 8))
        {
            var read = new List<(uint, string)>();
            while (records.MoveNext())
            {
                read.Add((records.ReadUInt32(), strings.Get(records.ReadUInt32())));
            }

            Assert.Equal([(1u, "Basic Road"), (2u, "Oak"), (3u, "Basic Road")], read);
        }

        DumpSection? terrainSection = reader.FindSection("terrain");
        Assert.NotNull(terrainSection);
        Assert.Null(terrainSection.RecordCount);
        Assert.True(terrainSection.HasFile("heights/raw.bin"));
        using (RecordFileReader heights = terrainSection.OpenRecordFile("heights/raw.bin", "height", 2))
        {
            Assert.Equal((ushort[])[0, 64, 65535, 1024], heights.ReadAllRecords<ushort>());
        }

        using JsonDocument grid2 = terrainSection.ReadJson("grid.json");
        Assert.Equal(16f, grid2.RootElement.GetProperty("spacingMeters").GetSingle());
    }

    [Fact]
    public void RefusesAFolderThatIsNotEmpty()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.PathOf("other.txt"), "x");

        Assert.Throws<IOException>(() => DumpWriter.Create(folder.Root, TestManifests.Minimal()));
    }

    [Fact]
    public void UsesAnEmptyFolderThatExists()
    {
        using var folder = new TempFolder();

        DumpWriter.Create(folder.Root, TestManifests.Minimal()).WriteManifest();

        Assert.Equal(TestManifests.MinimalJson, File.ReadAllText(folder.PathOf("manifest.json")));
    }

    [Fact]
    public void RejectsInvalidAndDuplicateSectionNames()
    {
        using var folder = new TempFolder();
        DumpWriter dump = DumpWriter.Create(folder.Root, TestManifests.Minimal());

        Assert.Throws<ArgumentException>(() => dump.BeginSection("Net_Nodes", new DumpVersion(0, 1)));
        Assert.Throws<ArgumentException>(() => dump.BeginSection("aux", new DumpVersion(0, 1)));
        Assert.Throws<ArgumentException>(() => dump.BeginSection(DumpProbes.FolderName, new DumpVersion(0, 1)));
        dump.BeginSection("terrain", new DumpVersion(0, 1));
        Assert.Throws<InvalidOperationException>(() => dump.BeginSection("terrain", new DumpVersion(0, 2)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape.bin")]
    [InlineData("/absolute.bin")]
    [InlineData("sub//double.bin")]
    [InlineData("Upper.bin")]
    [InlineData("back\\slash.bin")]
    [InlineData("trailing.")]
    [InlineData(".hidden")]
    [InlineData("nul.json")]
    [InlineData("lod/com1.bin")]
    public void RejectsInvalidFilePaths(string path)
    {
        using var folder = new TempFolder();
        SectionWriter section = DumpWriter.Create(folder.Root, TestManifests.Minimal()).BeginSection("terrain", new DumpVersion(0, 1));

        Assert.Throws<ArgumentException>(() => section.CreateRecordFile(path, "height", 2));
    }

    [Fact]
    public void LimitsPathsSoTheyFitWindowsPathLimits()
    {
        using var folder = new TempFolder();
        SectionWriter section = DumpWriter.Create(folder.Root, TestManifests.Minimal()).BeginSection("terrain", new DumpVersion(0, 1));

        // "terrain/" plus the path may have 120 characters.
        section.CreateJsonFile(new string('a', 112)).Dispose();
        Assert.Throws<ArgumentException>(() => section.CreateJsonFile(new string('b', 113)));
    }

    [Fact]
    public void RejectsTheSameFileTwice()
    {
        using var folder = new TempFolder();
        SectionWriter section = DumpWriter.Create(folder.Root, TestManifests.Minimal()).BeginSection("terrain", new DumpVersion(0, 1));
        using (RecordFileWriter first = section.CreateRecordFile("heights.bin", "height", 2))
        {
            first.Finish();
        }

        Assert.Throws<InvalidOperationException>(() => section.CreateRecordFile("heights.bin", "height", 2));
    }

    [Fact]
    public void CompletesASectionOnlyWhenItsFilesAreFinishedAndClosed()
    {
        using var folder = new TempFolder();
        DumpWriter dump = DumpWriter.Create(folder.Root, TestManifests.Minimal());
        SectionWriter section = dump.BeginSection("terrain", new DumpVersion(0, 1));
        RecordFileWriter heights = section.CreateRecordFile("heights.bin", "height", 2);
        JsonWriter grid = section.CreateJsonFile("grid.json");

        Assert.Throws<InvalidOperationException>(section.Complete);
        heights.Finish();
        Assert.Throws<InvalidOperationException>(section.Complete);
        heights.Dispose();
        grid.WriteStartObject();
        Assert.Throws<InvalidOperationException>(section.Complete);
        grid.WriteEndObject();
        Assert.Throws<InvalidOperationException>(section.Complete);
        grid.Dispose();
        section.Complete();

        SectionInfo info = Assert.Single(dump.Manifest.Sections);
        Assert.Equal([64L, 3L], info.Files.Select(file => file.Bytes));
        Assert.Throws<InvalidOperationException>(section.Complete);
        Assert.Throws<InvalidOperationException>(() => section.CreateJsonFile("more.json"));
    }

    [Fact]
    public void LeavesSectionsThatWereNeverCompletedOutOfTheManifest()
    {
        using var folder = new TempFolder();
        DumpWriter dump = DumpWriter.Create(folder.Root, TestManifests.Minimal());
        SectionWriter failed = dump.BeginSection("trees", new DumpVersion(0, 1));

        // The export of this section fails before its file is finished.
        failed.CreateRecordFile("trees.bin", "tree", 16).Dispose();
        dump.WriteManifest();

        DumpReader reader = DumpReader.Open(folder.Root);
        Assert.Empty(reader.Sections);
        Assert.True(File.Exists(folder.PathOf("trees", "trees.bin")));
    }

    [Fact]
    public void RefusesToCompleteASectionWhoseRecordFileWasClosedUnfinished()
    {
        using var folder = new TempFolder();
        DumpWriter dump = DumpWriter.Create(folder.Root, TestManifests.Minimal());
        SectionWriter section = dump.BeginSection("trees", new DumpVersion(0, 1));
        section.CreateRecordFile("trees.bin", "tree", 16).Dispose();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(section.Complete);

        Assert.Contains("'trees.bin' isn't finished", error.Message, StringComparison.Ordinal);
        Assert.Empty(dump.Manifest.Sections);
    }
}
