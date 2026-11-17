using System;
using System.Text;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class ManifestReaderTests
{
    private const string Source = "/dumps/seaside/manifest.json";

    [Fact]
    public void ReadsBackEveryFieldThatTheWriterWrote()
    {
        DumpManifest manifest = ManifestReader.Parse(TestManifests.FullJson, Source);

        Assert.Equal(new DumpVersion(0, 1), manifest.FormatVersion);
        Assert.True(manifest.Complete);
        Assert.Equal(TestManifests.CreatedUtc, manifest.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, manifest.CreatedUtc.Kind);
        Assert.Equal("CsToBeamNG.Exporter", manifest.Exporter.Name);
        Assert.Equal("0.1.0", manifest.Exporter.Version);
        Assert.Equal("1.17.1-f4", manifest.Game.Version);
        Assert.Collection(
            manifest.Game.Mods,
            mod =>
            {
                Assert.Equal("81 Tiles 2", mod.Name);
                Assert.Equal("2862121823", mod.WorkshopId);
            },
            mod =>
            {
                Assert.Equal("Local Mod", mod.Name);
                Assert.Null(mod.WorkshopId);
            });
        Assert.Equal("Seaside", manifest.City.Name);
        Assert.Equal("Green Plains", manifest.City.MapName);
        Assert.Equal("Sunny", manifest.City.Environment);
        Assert.Equal("Alpine", manifest.City.MapTheme);
        Assert.True(manifest.City.LeftHandTraffic);
        Assert.Equal([new TileCoordinate(4, 4), new TileCoordinate(4, 5)], manifest.City.UnlockedTiles);
        Assert.Equal(1234, manifest.Counts["netNodes"]);
        Assert.Equal(567, manifest.Counts["buildings"]);
        Assert.Equal(2, manifest.FailureCounts["props"]);
        Assert.Equal(["2 props were skipped: their prefab is missing."], manifest.Warnings);
        Assert.Collection(
            manifest.Sections,
            section =>
            {
                Assert.Equal("net-nodes", section.Name);
                Assert.Equal(new DumpVersion(0, 1), section.Version);
                Assert.Equal(1234, section.RecordCount);
                Assert.Equal("nodes.bin", section.Files[0].Path);
                Assert.Equal(59296, section.Files[0].Bytes);
                Assert.Equal("strings.json", section.Files[1].Path);
            },
            section =>
            {
                Assert.Equal("terrain", section.Name);
                Assert.Equal(new DumpVersion(1, 2), section.Version);
                Assert.Null(section.RecordCount);
                Assert.Equal("heights/raw.bin", Assert.Single(section.Files).Path);
            });

        // Writing it again gives the same text.
        Assert.Equal(TestManifests.FullJson, ManifestWriter.WriteToString(manifest));
    }

    [Fact]
    public void ReadsAManifestWithOnlyTheRequiredFields()
    {
        const string Json = """
            {
              "formatVersion": "0.1",
              "complete": false,
              "createdUtc": "2026-11-17T09:30:00Z",
              "exporter": { "name": "x", "version": "1" },
              "game": { "version": "1.17.1-f4" },
              "city": { "name": "", "leftHandTraffic": false },
              "sections": []
            }
            """;

        DumpManifest manifest = ManifestReader.Parse(Json, Source);

        Assert.False(manifest.Complete);
        Assert.Empty(manifest.Game.Mods);
        Assert.Equal(string.Empty, manifest.City.Name);
        Assert.Null(manifest.City.MapName);
        Assert.Null(manifest.City.Environment);
        Assert.Null(manifest.City.MapTheme);
        Assert.Empty(manifest.City.UnlockedTiles);
        Assert.Empty(manifest.Counts);
        Assert.Empty(manifest.FailureCounts);
        Assert.Empty(manifest.Warnings);
        Assert.Empty(manifest.Sections);
    }

    [Fact]
    public void TreatsNullAsAbsentForOptionalFields()
    {
        string json = ReplaceOnce(TestManifests.MinimalJson, "\"mods\": []", "\"mods\": null");
        json = ReplaceOnce(json, "\"name\": \"Seaside\",", "\"name\": \"Seaside\",\n    \"mapName\": null,");

        DumpManifest manifest = ManifestReader.Parse(json, Source);

        Assert.Empty(manifest.Game.Mods);
        Assert.Null(manifest.City.MapName);
    }

    [Fact]
    public void IgnoresUnknownFieldsFromNewerMinorVersions()
    {
        string json = ReplaceOnce(TestManifests.FullJson, "\"formatVersion\": \"0.1\",", "\"formatVersion\": \"0.7\",\n  \"futureField\": { \"nested\": [1, 2] },");
        json = ReplaceOnce(json, "\"leftHandTraffic\": true,", "\"leftHandTraffic\": true,\n    \"weather\": \"rain\",");
        json = ReplaceOnce(json, "\"bytes\": 120", "\"bytes\": 120,\n          \"checksum\": \"abc\"");

        DumpManifest manifest = ManifestReader.Parse(json, Source);

        Assert.Equal(new DumpVersion(0, 7), manifest.FormatVersion);
        Assert.True(manifest.City.LeftHandTraffic);
        Assert.Equal(120, manifest.Sections[0].Files[1].Bytes);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("12.3")]
    public void RejectsAnotherMajorVersionBeforeReadingFurther(string version)
    {
        // The rest of a manifest with another major version may follow another schema.
        string json = "{ \"formatVersion\": \"" + version + "\", \"somethingElse\": true }";

        DumpException error = Assert.Throws<DumpException>(() => ManifestReader.Parse(json, Source));

        Assert.Equal(DumpErrorKind.Incompatible, error.Kind);
        Assert.Contains(Source, error.Message, StringComparison.Ordinal);
        Assert.Contains("format version " + version, error.Message, StringComparison.Ordinal);
        Assert.Contains("reads version 0.x", error.Message, StringComparison.Ordinal);
        Assert.Contains("update CsToBeamNG", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"formatVersion\": \"0.1\",", "", "$.formatVersion")]
    [InlineData("\"formatVersion\": \"0.1\"", "\"formatVersion\": \"1\"", "$.formatVersion")]
    [InlineData("\"formatVersion\": \"0.1\"", "\"formatVersion\": 0.1", "$.formatVersion")]
    [InlineData("\"complete\": true", "\"complete\": \"yes\"", "$.complete")]
    [InlineData("\"createdUtc\": \"2026-11-17T09:30:00Z\"", "\"createdUtc\": \"2026-11-17 09:30\"", "$.createdUtc")]
    [InlineData("\"name\": \"CsToBeamNG.Exporter\",", "", "$.exporter.name")]
    [InlineData("\"name\": \"81 Tiles 2\",", "", "$.game.mods[0].name")]
    [InlineData("\"workshopId\": \"2862121823\"", "\"workshopId\": 2862121823", "$.game.mods[0].workshopId")]
    [InlineData("\"leftHandTraffic\": true,", "", "$.city.leftHandTraffic")]
    [InlineData("\"z\": 5", "\"z\": 9", "$.city.unlockedTiles[1].z")]
    [InlineData("\"buildings\": 567", "\"buildings\": -1", "$.counts.buildings")]
    [InlineData("\"buildings\": 567", "\"buildings\": 1.5", "$.counts.buildings")]
    [InlineData("\"props\": 2", "\"props\": \"2\"", "$.failureCounts.props")]
    [InlineData("\"2 props were skipped: their prefab is missing.\"", "2", "$.warnings[0]")]
    [InlineData("\"sections\": [", "\"sectionList\": [", "$.sections")]
    [InlineData("\"name\": \"net-nodes\"", "\"name\": \"Net_Nodes\"", "$.sections[0].name")]
    [InlineData("\"name\": \"terrain\"", "\"name\": \"net-nodes\"", "used twice")]
    [InlineData("\"version\": \"1.2\"", "\"version\": \"v1\"", "$.sections[1].version")]
    [InlineData("\"recordCount\": 1234", "\"recordCount\": -5", "$.sections[0].recordCount")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"../nodes.bin\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"/nodes.bin\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"Nodes.bin\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"nodes.\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"con.bin\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"nodes.bin\"", "\"path\": \"heights\\\\raw.bin\"", "$.sections[0].files[0].path")]
    [InlineData("\"path\": \"strings.json\"", "\"path\": \"nodes.bin\"", "listed twice")]
    [InlineData("\"bytes\": 59296", "\"bytes\": null", "$.sections[0].files[0].bytes")]
    public void RejectsInvalidFieldsNamingTheirLocation(string original, string replacement, string expectedInMessage)
    {
        string json = ReplaceOnce(TestManifests.FullJson, original, replacement);

        DumpException error = Assert.Throws<DumpException>(() => ManifestReader.Parse(json, Source));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains(Source, error.Message, StringComparison.Ordinal);
        Assert.Contains(expectedInMessage, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"manifest\"")]
    public void RejectsTextThatIsNotAJsonObject(string json)
    {
        DumpException error = Assert.Throws<DumpException>(() => ManifestReader.Parse(json, Source));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains(Source, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"complete\": true,", "\"complete\": true,\n  \"complete\": false,")]
    [InlineData("\"warnings\": [", "// A comment\n  \"warnings\": [")]
    [InlineData("\"props\": 2", "\"props\": 2,")]
    public void RejectsDuplicatePropertiesCommentsAndTrailingCommas(string original, string replacement)
    {
        string json = ReplaceOnce(TestManifests.FullJson, original, replacement);

        DumpException error = Assert.Throws<DumpException>(() => ManifestReader.Parse(json, Source));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("not valid JSON", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsAByteOrderMark()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(TestManifests.MinimalJson)];

        Assert.Equal("Seaside", ManifestReader.Parse(bytes, Source).City.Name);
    }

    [Fact]
    public void AcceptsAFractionOfASecond()
    {
        string json = ReplaceOnce(TestManifests.MinimalJson, "09:30:00Z", "09:30:00.25Z");

        Assert.Equal(TestManifests.CreatedUtc.AddMilliseconds(250), ManifestReader.Parse(json, Source).CreatedUtc);
    }

    [Fact]
    public void ReadFileReportsAMissingFile()
    {
        using var folder = new TempFolder();

        DumpException error = Assert.Throws<DumpException>(() => ManifestReader.ReadFile(folder.PathOf("manifest.json")));

        Assert.Equal(DumpErrorKind.NotFound, error.Kind);
    }

    private static string ReplaceOnce(string text, string original, string replacement)
    {
        int index = text.IndexOf(original, StringComparison.Ordinal);
        Assert.True(
            index >= 0 && text.IndexOf(original, index + 1, StringComparison.Ordinal) < 0,
            "The text to replace must occur exactly once: " + original);
        return string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + original.Length));
    }
}
