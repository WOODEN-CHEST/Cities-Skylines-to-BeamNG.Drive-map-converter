using System;
using System.IO;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class ManifestWriterTests
{
    [Fact]
    public void WritesEveryFieldInTheDocumentedLayout()
    {
        Assert.Equal(TestManifests.FullJson, ManifestWriter.WriteToString(TestManifests.Full()));
    }

    [Fact]
    public void LeavesOutAbsentOptionalValuesButWritesEmptyCollections()
    {
        Assert.Equal(TestManifests.MinimalJson, ManifestWriter.WriteToString(TestManifests.Minimal()));
    }

    [Theory]
    [InlineData("lv-LV")]
    [InlineData("de-DE")]
    public void WritesTheSameTextUnderDecimalCommaCultures(string culture)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(TestManifests.FullJson, ManifestWriter.WriteToString(TestManifests.Full()));
    }

    [Theory]
    [InlineData("local time", "createdUtc")]
    [InlineData("no exporter name", "exporter.name")]
    [InlineData("no exporter version", "exporter.version")]
    [InlineData("no game version", "game.version")]
    [InlineData("unnamed mod", "game.mods[0].name")]
    [InlineData("workshop id with letters", "game.mods[0].workshopId")]
    [InlineData("count key with a space", "invalid key 'net nodes'")]
    [InlineData("negative count", "counts.buildings")]
    [InlineData("negative failure count", "failureCounts.props")]
    [InlineData("null warning", "warnings[0]")]
    [InlineData("section name with capitals", "sections[0].name")]
    [InlineData("section name used twice", "used twice")]
    [InlineData("negative record count", "sections[0].recordCount")]
    [InlineData("file path leaving the section", "sections[0].files[0].path")]
    [InlineData("file listed twice", "twice")]
    [InlineData("negative file size", "sections[0].files[0].bytes")]
    public void RejectsInvalidManifests(string mistake, string expectedInMessage)
    {
        DumpManifest manifest = TestManifests.Full();
        MakeMistake(manifest, mistake);

        ArgumentException error = Assert.Throws<ArgumentException>(() => ManifestWriter.WriteToString(manifest));

        Assert.Contains(expectedInMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFileReplacesTheManifestAndLeavesNoTemporaryFile()
    {
        using var folder = new TempFolder();
        string path = folder.PathOf("manifest.json");
        DumpManifest manifest = TestManifests.Minimal();
        manifest.Complete = false;
        ManifestWriter.WriteFile(manifest, path);
        Assert.Contains("\"complete\": false", File.ReadAllText(path), StringComparison.Ordinal);

        manifest.Complete = true;
        ManifestWriter.WriteFile(manifest, path);

        Assert.Equal(TestManifests.MinimalJson, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    private static void MakeMistake(DumpManifest manifest, string mistake)
    {
        switch (mistake)
        {
            case "local time":
                manifest.CreatedUtc = new DateTime(2026, 11, 17, 9, 30, 0, DateTimeKind.Local);
                break;
            case "no exporter name":
                manifest.Exporter.Name = string.Empty;
                break;
            case "no exporter version":
                manifest.Exporter.Version = string.Empty;
                break;
            case "no game version":
                manifest.Game.Version = string.Empty;
                break;
            case "unnamed mod":
                manifest.Game.Mods[0].Name = string.Empty;
                break;
            case "workshop id with letters":
                manifest.Game.Mods[0].WorkshopId = "28621x";
                break;
            case "count key with a space":
                manifest.Counts["net nodes"] = 1;
                break;
            case "negative count":
                manifest.Counts["buildings"] = -1;
                break;
            case "negative failure count":
                manifest.FailureCounts["props"] = -1;
                break;
            case "null warning":
                manifest.Warnings[0] = null!;
                break;
            case "section name with capitals":
                manifest.Sections[0].Name = "Net-Nodes";
                break;
            case "section name used twice":
                manifest.Sections[1].Name = "net-nodes";
                break;
            case "negative record count":
                manifest.Sections[0].RecordCount = -1;
                break;
            case "file path leaving the section":
                manifest.Sections[0].Files[0].Path = "../nodes.bin";
                break;
            case "file listed twice":
                manifest.Sections[0].Files[1].Path = "nodes.bin";
                break;
            case "negative file size":
                manifest.Sections[0].Files[0].Bytes = -1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mistake), mistake, "Unknown mistake.");
        }
    }
}
