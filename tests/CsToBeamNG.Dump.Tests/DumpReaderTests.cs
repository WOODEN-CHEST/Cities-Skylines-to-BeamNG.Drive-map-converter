using System;
using System.IO;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpReaderTests
{
    [Fact]
    public void ReportsAMissingFolder()
    {
        using var folder = new TempFolder();

        DumpException error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.PathOf("missing")));

        Assert.Equal(DumpErrorKind.NotFound, error.Kind);
        Assert.Contains("doesn't exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAFolderWithoutAManifest()
    {
        using var folder = new TempFolder();

        DumpException error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root));
        Assert.Equal(DumpErrorKind.NotFound, error.Kind);
        Assert.Contains("is not a city dump", error.Message, StringComparison.Ordinal);

        File.WriteAllText(folder.PathOf("manifest.json.tmp"), "{");
        error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root));
        Assert.Contains("interrupted", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnIncompleteDumpUnlessAllowed()
    {
        using var folder = new TempFolder();
        WriteDump(folder, complete: false);

        DumpException error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root));
        Assert.Equal(DumpErrorKind.Incomplete, error.Kind);
        Assert.Contains("Export the city again", error.Message, StringComparison.Ordinal);

        DumpReader reader = DumpReader.Open(folder.Root, new DumpReadOptions { AllowIncomplete = true });
        DumpNotice notice = Assert.Single(reader.Notices);
        Assert.Equal(DumpNoticeSeverity.Warning, notice.Severity);
        Assert.Single(reader.Sections);
    }

    [Fact]
    public void RejectsAMissingSectionFile()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        File.Delete(folder.PathOf("terrain", "heights.bin"));

        DumpException error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("heights.bin' is missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsASectionFileOfAnotherSize()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        File.AppendAllText(folder.PathOf("terrain", "heights.bin"), "x");

        DumpException error = Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("has 69 bytes, but the manifest lists 68", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnotherMajorFormatVersion()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        ChangeManifest(folder, "\"formatVersion\": \"0.1\"", "\"formatVersion\": \"1.0\"");

        Assert.Equal(DumpErrorKind.Incompatible, Assert.Throws<DumpException>(() => DumpReader.Open(folder.Root)).Kind);
    }

    [Fact]
    public void NotesANewerMinorFormatVersion()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        ChangeManifest(folder, "\"formatVersion\": \"0.1\"", "\"formatVersion\": \"0.5\"");

        DumpReader reader = DumpReader.Open(folder.Root);

        Assert.Equal(new DumpVersion(0, 5), reader.FormatVersion);
        DumpNotice notice = Assert.Single(reader.Notices);
        Assert.Equal(DumpNoticeSeverity.Info, notice.Severity);
        Assert.Contains("0.5", notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoresFilesThatTheManifestDoesNotList()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        File.WriteAllText(folder.PathOf("export.log"), "log");
        File.WriteAllText(folder.PathOf("terrain", "extra.json"), "{}");

        DumpSection? section = DumpReader.Open(folder.Root).FindSection("terrain");

        Assert.NotNull(section);
        Assert.False(section.HasFile("extra.json"));
        Assert.Equal(DumpErrorKind.Corrupt, Assert.Throws<DumpException>(() => section.GetFilePath("extra.json")).Kind);
    }

    [Fact]
    public void ChecksTheRecordTypeOfSectionFiles()
    {
        using var folder = new TempFolder();
        WriteDump(folder);
        DumpSection? section = DumpReader.Open(folder.Root).FindSection("terrain");
        Assert.NotNull(section);

        using (RecordFileReader heights = section.OpenRecordFile("heights.bin", "height", 2))
        {
            Assert.Equal(2, heights.RecordCount);
        }

        Assert.Equal(DumpErrorKind.Corrupt, Assert.Throws<DumpException>(() => section.OpenRecordFile("heights.bin", "netNode", 2)).Kind);
    }

    // A dump with one section, "terrain", holding heights.bin: two 2-byte records, 68 bytes.
    private static void WriteDump(TempFolder folder, bool complete = true)
    {
        DumpManifest manifest = TestManifests.Minimal();
        manifest.Complete = complete;
        DumpWriter dump = DumpWriter.Create(folder.Root, manifest);
        SectionWriter terrain = dump.BeginSection("terrain", new DumpVersion(0, 1));
        using (RecordFileWriter heights = terrain.CreateRecordFile("heights.bin", "height", 2))
        {
            foreach (ushort height in (ushort[])[1, 2])
            {
                heights.BeginRecord();
                heights.WriteUInt16(height);
                heights.EndRecord();
            }

            heights.Finish();
        }

        terrain.Complete();
        dump.WriteManifest();
    }

    private static void ChangeManifest(TempFolder folder, string original, string replacement)
    {
        string path = folder.PathOf("manifest.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace(original, replacement, StringComparison.Ordinal));
    }
}
