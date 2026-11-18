using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CsToBeamNG.Converter.BeamNG.Terrain;
using CsToBeamNG.Converter.Conversion;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Tests.BeamNG;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Conversion;

public sealed class CityLevelConverterTests : IDisposable
{
    private const string Level = "cstobeamng_test";
    private const string LevelFolder = "/levels/" + Level;

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ProducesTheLevelFiles()
    {
        ConversionResult result = Convert(LoadCity());

        Assert.Equal(Level, result.LevelName);
        Assert.Equal(
            [
                LevelFolder + "/art/terrains/main.materials.json",
                LevelFolder + "/cstobeamng-report.txt",
                LevelFolder + "/info.json",
                LevelFolder + "/main/MissionGroup/PlayerDropPoints/items.level.json",
                LevelFolder + "/main/MissionGroup/items.level.json",
                LevelFolder + "/main/MissionGroup/level_objects/items.level.json",
                LevelFolder + "/main/MissionGroup/level_objects/sky_and_sun/items.level.json",
                LevelFolder + "/main/MissionGroup/level_objects/terrain/items.level.json",
                LevelFolder + "/main/MissionGroup/level_objects/water/items.level.json",
                LevelFolder + "/main/items.level.json",
                LevelFolder + "/terrain.ter",
                LevelFolder + "/terrain.terrain.json",
            ],
            result.Files.Select(file => file.Path.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void StoresTheCitysGroundInTheTerrain()
    {
        WorldLoadResult loaded = LoadCity();
        CityTerrain terrain = loaded.World.Find<CityTerrain>()!;
        ConversionResult result = Convert(loaded);

        JsonElement block = Line(result, "level_objects/terrain", "TerrainBlock");
        float[] position = [.. block.GetProperty("position").EnumerateArray().Select(v => v.GetSingle())];
        float maxHeight = block.GetProperty("maxHeight").GetSingle();
        Assert.Equal([-64f, -64f], position[..2]);
        Assert.Equal(1, block.GetProperty("squareSize").GetSingle());
        Assert.Equal(LevelFolder + "/terrain.ter", block.GetProperty("terrainFile").GetString());
        Assert.Equal(128, block.GetProperty("baseTexSize").GetInt32());

        TerrainData data = TerFile.Read(new MemoryStream(result.Files.Single(LevelFolder + "/terrain.ter").ToBytes()));
        Assert.Equal(128, data.Size);
        Assert.Equal(["grass", "dirt", "rock", "sand", "asphalt"], data.MaterialNames);
        var range = new TerrainHeightRange(position[2], maxHeight);
        for (int row = 0; row < data.Size; row += 3)
        {
            for (int column = 0; column < data.Size; column += 3)
            {
                double expected = terrain.SampleGround(-64 + column, -64 + row, CoarseInterpolation.MonotoneCubic);
                Assert.Equal(expected, range.ToMeters(data.GetHeight(column, row)), (range.StepMeters / 2) + 1e-4);
            }
        }

        // The materials: sand under the sea in the west, grass on the flat land in the east.
        Assert.Equal((byte)SurfaceKind.Sand, data.GetMaterial(4, 64));
        Assert.Equal((byte)SurfaceKind.Grass, data.GetMaterial(100, 64));
    }

    [Fact]
    public void AddsTheSeaAndASpawnPointOnDryGround()
    {
        ConversionResult result = Convert(LoadCity());

        JsonElement sea = Line(result, "level_objects/water", "WaterPlane");
        Assert.Equal(40 - 0.3125, sea.GetProperty("position")[2].GetDouble(), 6);
        JsonElement spawn = Line(result, "PlayerDropPoints", "SpawnSphere");
        Assert.Equal(CityLevelConverter.SpawnPointName, spawn.GetProperty("name").GetString());
        Assert.Equal([0.0, 0, 45.5], spawn.GetProperty("position").EnumerateArray().Select(v => Math.Round(v.GetDouble(), 3)));
        Assert.True(result.Report.SpawnOnDryGround);

        using JsonDocument info = JsonDocument.Parse(result.Files.Single(LevelFolder + "/info.json").Text());
        Assert.Equal("Synthetic City", info.RootElement.GetProperty("title").GetString());
        Assert.Equal(CityLevelConverter.SpawnPointName, info.RootElement.GetProperty("defaultSpawnPointName").GetString());
        Assert.False(info.RootElement.GetProperty("supportsTraffic").GetBoolean());
        Assert.Equal([128, 128], info.RootElement.GetProperty("size").EnumerateArray().Select(v => v.GetInt32()));
    }

    [Fact]
    public void IsByteIdenticalAcrossRunsAndCultures()
    {
        WorldLoadResult loaded = LoadCity();
        Dictionary<string, byte[]> first = OutputFiles.Snapshot(Convert(loaded).Files);

        Assert.Equal(first, OutputFiles.Snapshot(Convert(loaded).Files));
        using (new CultureScope("lv-LV"))
        {
            Assert.Equal(first, OutputFiles.Snapshot(Convert(loaded).Files));
        }
    }

    [Fact]
    public void ReportsWhatItMadeAndWhatItLeftOut()
    {
        using var scope = new CultureScope("lv-LV");
        ConversionResult result = Convert(LoadCity());

        string report = result.Files.Single(LevelFolder + "/" + ConversionReport.FileName).Text();
        Assert.Equal(result.Report.ToText(), report);
        Assert.Contains("Level:   cstobeamng_test\n", report, StringComparison.Ordinal);
        Assert.Contains("City:    Synthetic City on the map Test Map", report, StringComparison.Ordinal);
        Assert.Contains("Area:    x -60 to 60, z -60 to 60 (120 × 120 m), the test area\n", report, StringComparison.Ordinal);
        Assert.Contains("  Grid:      128 × 128 samples, 1 m apart, 127 m across, covering x -64 to 63, z -64 to 63\n", report, StringComparison.Ordinal);
        Assert.Contains("  Sea:       a water plane at 39.688 m (the sea level 40 m, drawn 0.3125 m lower)", report, StringComparison.Ordinal);
        Assert.Contains("  Lakes:     0\n", report, StringComparison.Ordinal);
        Assert.Contains("Spawn point: spawn_centre at (0, 0, 45.5), the dry, level ground nearest the area's middle\n", report, StringComparison.Ordinal);
        Assert.Contains("Not converted yet: 12 buildings (step 1.9)\n", report, StringComparison.Ordinal);
        Assert.EndsWith("Notes\n  none\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsASingleDryCellBelowTheSeaInTheSingular()
    {
        // One cell of the sea floor without water, as where a building stands in the sea.
        ConversionResult result = Convert(LoadCity(depth: (x, z) => x == -48 && z == 0 ? 0 : SeaDepth(x, z)));

        Assert.Equal(1, result.Report.Water!.DryCellsBelowSea);
        Assert.Contains(
            "\n             1 dry cell (0 km²) lies below the sea and is flooded; the game keeps it dry, probably because a building stands there\n",
            result.Report.ToText(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertsACityWithoutWater()
    {
        ConversionResult result = Convert(LoadCity(withWater: false));

        Assert.DoesNotContain(result.Files, file => file.Path.Value.Contains("/water/", StringComparison.Ordinal));
        Assert.Contains("warning: the dump has no water, so the level has none.", result.Report.Notes);
        Assert.Null(result.Report.Water);
    }

    [Fact]
    public void RefusesADumpWithoutTerrain()
    {
        string folder = new SyntheticDump().WithWater(TestTerrain.CreateWater(40, (x, z) => 30, (x, z) => 10)).WriteTo(_temp.PathOf("water-only"));

        ConversionException error = Assert.Throws<ConversionException>(() => Convert(WorldLoader.Load(folder)));
        Assert.Contains("no terrain", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAnAreaTooLargeForTheTerrain()
    {
        ConversionResult result = CityLevelConverter.Convert(LoadCity(), Options() with
        {
            TerrainGrid = new TerrainGridOptions { MinSize = 32, MaxSize = 32, SquareSizes = [1] },
        });

        Assert.False(result.Report.Grid.CoversArea);
        Assert.Contains(result.Report.Notes, note => note.StartsWith("warning: the area is larger than a terrain of 32 samples", StringComparison.Ordinal));
    }

    // Sea floor at 30 m in the west, rising to flat land at 45 m from x = −26; the ground tilts gently north.
    private static double Ground(double x, double z) => 30 + (Math.Clamp(x + 56, 0, 30) * 0.5) + (z / 64);

    private static ConversionOptions Options() => new()
    {
        LevelName = Level,
        Area = new MapRect(-60, -60, 60, 60),
        AreaDescription = "the test area",
        TerrainGrid = new TerrainGridOptions { MaxSize = 256 },
    };

    private static ConversionResult Convert(WorldLoadResult loaded) => CityLevelConverter.Convert(loaded, Options());

    private static JsonElement Line(ConversionResult result, string group, string className)
    {
        string text = result.Files.Single(LevelFolder + "/main/MissionGroup/" + group + "/items.level.json").Text();
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonElement element = JsonDocument.Parse(line).RootElement;
            if (element.GetProperty("class").GetString() == className)
            {
                return element.Clone();
            }
        }

        throw new InvalidOperationException($"No {className} in {group}.");
    }

    // The sea: water up to 40 m west of x = −40.
    private static double SeaDepth(double x, double z) => x <= -40 ? Math.Max(0, 40 - Ground(x, z)) : 0;

    private WorldLoadResult LoadCity(bool withWater = true, Func<double, double, double>? depth = null)
    {
        var dump = new SyntheticDump()
            .WithUnlockedTiles(new TileCoordinate(4, 4))
            .WithTerrain(TestTerrain.Create(Ground, Ground).WithDetail(new TileCoordinate(4, 4), Ground));
        if (withWater)
        {
            dump.WithWater(TestTerrain.CreateWater(40, Ground, depth ?? SeaDepth));
        }

        return WorldLoader.Load(dump.WriteTo(_temp.PathOf(withWater ? "dump" : "dry-dump")));
    }
}
