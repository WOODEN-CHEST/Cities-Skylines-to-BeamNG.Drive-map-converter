using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Terrain;
using CsToBeamNG.Converter.Development;
using CsToBeamNG.Converter.Tests.BeamNG;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Development;

public sealed class HelloLevelTests
{
    [Fact]
    public void ProducesTheLevelFiles()
    {
        IReadOnlyList<OutputFile> files = HelloLevel.CreateFiles();

        Assert.Equal(
            [
                "/levels/cstobeamng_hello/art/shapes/hello_box.dae",
                "/levels/cstobeamng_hello/art/shapes/main.materials.json",
                "/levels/cstobeamng_hello/art/terrains/main.materials.json",
                "/levels/cstobeamng_hello/info.json",
                "/levels/cstobeamng_hello/main/MissionGroup/PlayerDropPoints/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/ai_roads/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/level_objects/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/level_objects/sky_and_sun/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/level_objects/terrain/items.level.json",
                "/levels/cstobeamng_hello/main/MissionGroup/objects/items.level.json",
                "/levels/cstobeamng_hello/main/items.level.json",
                "/levels/cstobeamng_hello/terrain.ter",
                "/levels/cstobeamng_hello/terrain.terrain.json",
            ],
            files.Select(file => file.Path.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void IsByteIdenticalAcrossRunsAndCultures()
    {
        Dictionary<string, byte[]> first = OutputFiles.Snapshot(HelloLevel.CreateFiles());

        Assert.Equal(first, OutputFiles.Snapshot(HelloLevel.CreateFiles()));
        using (new CultureScope("lv-LV"))
        {
            Assert.Equal(first, OutputFiles.Snapshot(HelloLevel.CreateFiles()));
        }
    }

    [Fact]
    public void ReferencesOnlyFilesItWritesOrThatShipWithTheGame()
    {
        IReadOnlyList<OutputFile> files = HelloLevel.CreateFiles();
        var written = files.Select(file => file.Path.Value).ToHashSet(StringComparer.Ordinal);
        var references = new List<string>();
        foreach (OutputFile file in files.Where(file => file.Path.Name == "items.level.json"))
        {
            foreach (string line in file.Text().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using JsonDocument document = JsonDocument.Parse(line);
                foreach (string field in new[] { "terrainFile", "shapeName" })
                {
                    if (document.RootElement.TryGetProperty(field, out JsonElement value))
                    {
                        references.Add(value.GetString()!);
                    }
                }
            }
        }

        Assert.Equal(
            ["/levels/cstobeamng_hello/art/shapes/hello_box.dae", "/levels/cstobeamng_hello/terrain.ter"],
            references.Order(StringComparer.Ordinal));
        Assert.All(references, reference => Assert.Contains(reference, written));
    }

    [Fact]
    public void ShapesTheTerrainAsDesigned()
    {
        OutputFile terFile = HelloLevel.CreateFiles().Single("/levels/cstobeamng_hello/terrain.ter");
        TerrainData terrain = TerFile.Read(new MemoryStream(terFile.ToBytes()));
        var scale = new TerrainHeightScale(HelloLevel.MaxHeight);

        Assert.Equal(HelloLevel.TerrainSize, terrain.Size);
        Assert.Equal(["grass", "asphalt"], terrain.MaterialNames);

        // Sample (x + 512, y + 512) lies at world (x, y): the hill's top at (300, 150), flat ground where a
        // transposed or mirrored terrain would put it.
        Assert.Equal(scale.ToStored(HelloLevel.GroundHeight + HelloLevel.HillHeight), terrain.GetHeight(812, 662));
        Assert.Equal(scale.ToStored(HelloLevel.GroundHeight), terrain.GetHeight(662, 812));
        Assert.Equal(scale.ToStored(HelloLevel.GroundHeight), terrain.GetHeight(812, 362));

        // Asphalt under the road's west end, grass beside it.
        Assert.Equal(1, terrain.GetMaterial(62, 212));
        Assert.Equal(0, terrain.GetMaterial(62, 232));
    }

    [Fact]
    public void KeepsTheRoadAndTheBoxOnFlatGround()
    {
        foreach (var point in HelloLevel.RoadPoints)
        {
            Assert.Equal(HelloLevel.GroundHeight, HelloLevel.GroundHeightAt(point.X, point.Y));
        }

        Assert.Equal(HelloLevel.GroundHeight, HelloLevel.GroundHeightAt(HelloLevel.BoxPosition.X, HelloLevel.BoxPosition.Y));
        Assert.True(HelloLevel.DistanceToRoad(HelloLevel.SpawnPosition.X, HelloLevel.SpawnPosition.Y) < HelloLevel.RoadWidth / 2);
        Assert.True(HelloLevel.DistanceToRoad(HelloLevel.BoxPosition.X, HelloLevel.BoxPosition.Y) > 30);
    }
}
