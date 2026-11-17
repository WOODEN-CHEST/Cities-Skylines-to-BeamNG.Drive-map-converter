using System;
using System.Collections.Generic;
using System.IO;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class ManifestBuilderTests
{
    private static readonly DateTime _startUtc = new(2026, 11, 17, 13, 30, 5, DateTimeKind.Utc);

    [Fact]
    public void StartsWithAnIncompleteManifest()
    {
        DumpManifest manifest = CreateInitial([]);

        Assert.False(manifest.Complete);
        Assert.Equal(_startUtc, manifest.CreatedUtc);
        Assert.Equal("CsToBeamNG.Exporter", manifest.Exporter.Name);
        Assert.Equal("0.1.0+abc", manifest.Exporter.Version);
        Assert.Equal("1.21.1-f9", manifest.Game.Version);
        Assert.Equal("CityPalma", manifest.City.Name);
        Assert.Empty(manifest.Counts);
        Assert.Empty(manifest.Sections);
    }

    [Fact]
    public void SortsModsAndDropsWhatTheManifestWouldReject()
    {
        DumpManifest manifest = CreateInitial(
        [
            new GameModInfo("Zoning Tool", "123"),
            new GameModInfo("81 Tiles 2", "2862121823"),
            new GameModInfo(string.Empty, null),
            new GameModInfo("Broken Id", "12a"),
            new GameModInfo("81 Tiles 2", null),
        ]);

        Assert.Collection(
            manifest.Game.Mods,
            mod => AssertMod("(unnamed mod)", null, mod),
            mod => AssertMod("81 Tiles 2", null, mod),
            mod => AssertMod("81 Tiles 2", "2862121823", mod),
            mod => AssertMod("Broken Id", null, mod),
            mod => AssertMod("Zoning Tool", "123", mod));
    }

    [Fact]
    public void AddsTheSnapshot()
    {
        DumpManifest manifest = CreateInitial([]);
        CitySnapshot snapshot = CreateSnapshot();

        IList<string> problems = ManifestBuilder.AddSnapshot(manifest, snapshot);

        Assert.Empty(problems);
        Assert.Empty(manifest.Warnings);
        Assert.Equal("Palma de Mallorca", manifest.City.Name);
        Assert.Equal("Sunny Coast", manifest.City.MapName);
        Assert.Equal("Sunny", manifest.City.Environment);
        Assert.Null(manifest.City.MapTheme);
        Assert.True(manifest.City.LeftHandTraffic);
        Assert.Equal([new TileCoordinate(4, 4), new TileCoordinate(5, 4)], manifest.City.UnlockedTiles);
        Assert.Equal(
            new Dictionary<string, long> { ["buildings"] = 3, ["netNodes"] = 12 },
            manifest.Counts);
    }

    [Fact]
    public void TurnsGridProblemsIntoWarnings()
    {
        DumpManifest manifest = CreateInitial([]);
        CitySnapshot snapshot = CreateSnapshot();
        snapshot.AreaGrid = new int[7];

        IList<string> problems = ManifestBuilder.AddSnapshot(manifest, snapshot);

        string problem = Assert.Single(problems);
        Assert.Equal([problem], manifest.Warnings);
        Assert.Empty(manifest.City.UnlockedTiles);
    }

    [Fact]
    public void WritesAManifestThatTheReaderAccepts()
    {
        using var folder = new TempFolder();
        DumpManifest manifest = CreateInitial([new GameModInfo("81 Tiles 2", "2862121823")]);
        DumpWriter writer = DumpWriter.Create(folder.PathOf("dump"), manifest);
        writer.WriteManifest();
        Assert.Throws<DumpException>(() => DumpReader.Open(writer.Folder));

        ManifestBuilder.AddSnapshot(manifest, CreateSnapshot());
        manifest.Complete = true;
        writer.WriteManifest();
        DumpReader reader = DumpReader.Open(writer.Folder);

        DumpManifest read = reader.Manifest;
        Assert.True(read.Complete);
        Assert.Empty(reader.Notices);
        Assert.Equal("Palma de Mallorca", read.City.Name);
        Assert.Equal("Sunny Coast", read.City.MapName);
        Assert.Null(read.City.MapTheme);
        Assert.True(read.City.LeftHandTraffic);
        Assert.Equal(manifest.City.UnlockedTiles, read.City.UnlockedTiles);
        Assert.Equal(manifest.Counts, read.Counts);
        GameModInfo mod = Assert.Single(read.Game.Mods);
        AssertMod("81 Tiles 2", "2862121823", mod);
        Assert.True(File.Exists(Path.Join(writer.Folder, DumpFormat.ManifestFileName)));
    }

    private static DumpManifest CreateInitial(GameModInfo[] mods) =>
        ManifestBuilder.CreateInitial("0.1.0+abc", "1.21.1-f9", mods, "CityPalma", _startUtc);

    private static CitySnapshot CreateSnapshot()
    {
        int[] grid = new int[25];
        grid[12] = 1;
        grid[13] = 2;
        var snapshot = new CitySnapshot
        {
            CityName = "Palma de Mallorca",
            MapName = "Sunny Coast",
            Environment = "Sunny",
            MapTheme = string.Empty,
            LeftHandTraffic = true,
            AreaGrid = grid,
        };
        snapshot.Counts.Add(new ObjectCount(CountKeys.NetNodes, 12, 12, 32768));
        snapshot.Counts.Add(new ObjectCount(CountKeys.Buildings, 3, 4, 49152));
        return snapshot;
    }

    private static void AssertMod(string name, string? workshopId, GameModInfo mod)
    {
        Assert.Equal(name, mod.Name);
        Assert.Equal(workshopId, mod.WorkshopId);
    }
}
