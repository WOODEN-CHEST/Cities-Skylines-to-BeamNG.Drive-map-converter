using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Terrain;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class TerrainFilesTests
{
    private const string Level = "test_level";

    [Fact]
    public void DescribesTheTerFileLikeTheGamesOwnTerrainJson()
    {
        var terrain = new TerrainData(4, ["grass", "asphalt"]);
        OutputFile file = TerrainJsonFile.Create(
            VirtualPath.Parse("/levels/test_level/terrain.terrain.json"), VirtualPath.Parse("/levels/test_level/terrain.ter"), terrain);

        Assert.Equal(
            """
            {
              "binaryFormat": "version(char), size(unsigned int), heightMap(heightMapSize * heightMapItemSize), layerMap(layerMapSize * layerMapItemSize), layerTextureMap(layerMapSize * layerMapItemSize), materialNames",
              "datafile": "/levels/test_level/terrain.ter",
              "heightMapItemSize": 2,
              "heightMapSize": 16,
              "layerMapItemSize": 1,
              "layerMapSize": 16,
              "materials": [
                "grass",
                "asphalt"
              ],
              "size": 4,
              "version": 9
            }

            """,
            file.Text());
    }

    [Fact]
    public void NamesMaterialsAfterTheirInternalNameAndId()
    {
        TerrainMaterial grass = TerrainMaterialPresets.Grass("grass");

        // uuid.uuid5(namespace, "test_level:terrain-material:grass") in Python.
        Assert.Equal("grass-ca12646b-2da5-5628-bb63-69946c723964", TerrainMaterialsFile.GetObjectName(Level, grass));
    }

    [Fact]
    public void WritesMaterialsAndTheTextureSet()
    {
        JsonElement root = WriteMaterials(out _);

        Assert.Equal(
            ["asphalt-" + PersistentId.ForObject(Level, "terrain-material:asphalt"), "grass-ca12646b-2da5-5628-bb63-69946c723964", "test_TerrainMaterialTextureSet"],
            root.EnumerateObject().Select(property => property.Name));

        JsonElement grass = root.GetProperty("grass-ca12646b-2da5-5628-bb63-69946c723964");
        Assert.Equal(
            ["name", "internalName", "class", "persistentId"],
            grass.EnumerateObject().Take(4).Select(property => property.Name));
        Assert.Equal("TerrainMaterial", grass.GetProperty("class").GetString());
        Assert.Equal("grass", grass.GetProperty("internalName").GetString());
        Assert.Equal("ca12646b-2da5-5628-bb63-69946c723964", grass.GetProperty("persistentId").GetString());
        Assert.Equal("GRASS", grass.GetProperty("groundmodelName").GetString());
        Assert.Equal("/levels/template/art/terrains/t_terrain_base_grass_b.png", grass.GetProperty("baseColorBaseTex").GetString());
        Assert.Equal(512, grass.GetProperty("baseColorBaseTexSize").GetSingle());
        Assert.Equal(
            "/assets/materials/terrain/grass/ind_grass/t_ind_grass_nm.png", grass.GetProperty("normalDetailTex").GetString());
        Assert.Equal([0.7f, 0.15f], grass.GetProperty("normalDetailStrength").EnumerateArray().Select(value => value.GetSingle()));
        Assert.Equal([0f, 10, 100, 3000], grass.GetProperty("macroDistances").EnumerateArray().Select(value => value.GetSingle()));
        Assert.False(grass.TryGetProperty("baseColorDetailTexSize", out _));

        string[] rest = [.. grass.EnumerateObject().Skip(4).Select(property => property.Name)];
        Assert.Equal(rest.Order(StringComparer.Ordinal), rest);

        JsonElement textureSet = root.GetProperty("test_TerrainMaterialTextureSet");
        Assert.Equal("TerrainMaterialTextureSet", textureSet.GetProperty("class").GetString());
        Assert.Equal([512, 512], textureSet.GetProperty("baseTexSize").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal([1024, 1024], textureSet.GetProperty("detailTexSize").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal([1024, 1024], textureSet.GetProperty("macroTexSize").EnumerateArray().Select(value => value.GetInt32()));
    }

    [Fact]
    public void PresetsReferenceOnlyTexturesThatShipWithTheGame()
    {
        WriteMaterials(out string text);
        using JsonDocument document = JsonDocument.Parse(text);
        var textures = new List<string>();
        foreach (JsonProperty material in document.RootElement.EnumerateObject())
        {
            textures.AddRange(material.Value.EnumerateObject()
                .Where(property => property.Name.EndsWith("Tex", StringComparison.Ordinal))
                .Select(property => property.Value.GetString()!));
        }

        Assert.Equal(30, textures.Count);
        Assert.All(textures, texture => Assert.True(
            texture.StartsWith("/assets/materials/terrain/", StringComparison.Ordinal)
            || texture.StartsWith("/levels/template/art/terrains/t_terrain_base_", StringComparison.Ordinal),
            texture));
    }

    [Fact]
    public void RejectsDuplicateInternalNames()
    {
        TerrainMaterialTextureSet textureSet = TerrainMaterialPresets.TextureSet("test_TerrainMaterialTextureSet");

        Assert.Throws<ArgumentException>(() => TerrainMaterialsFile.Create(
            VirtualPath.Parse("/levels/test_level/art/terrains/main.materials.json"),
            Level,
            textureSet,
            [TerrainMaterialPresets.Grass("grass"), TerrainMaterialPresets.Asphalt("grass")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainMaterialTextureSet("set", 1000, 1024, 1024));
    }

    [Fact]
    public void WritesOptionalFieldsOnlyWhenSet()
    {
        var material = new TerrainMaterial
        {
            InternalName = "plain",
            GroundModelName = "DIRT",
            Base = new TerrainMaterialLayer { BaseColor = new TerrainMaterialMap(VirtualPath.Parse("/a/b.png")) },
            Macro = new TerrainMaterialLayer(),
            Detail = new TerrainMaterialLayer
            {
                TextureSize = 2,
                Normal = new TerrainMaterialMap(VirtualPath.Parse("/a/n.png")) { Strength = new Vector2(1, 0.5f) },
            },
        };
        OutputFile file = TerrainMaterialsFile.Create(
            VirtualPath.Parse("/levels/test_level/main.materials.json"), Level, TerrainMaterialPresets.TextureSet("set"), [material]);

        using JsonDocument document = JsonDocument.Parse(file.Text());
        JsonElement json = document.RootElement.GetProperty(TerrainMaterialsFile.GetObjectName(Level, material));
        Assert.Equal(
            ["name", "internalName", "class", "persistentId", "baseColorBaseTex", "groundmodelName", "normalDetailStrength", "normalDetailTex", "normalDetailTexSize"],
            json.EnumerateObject().Select(property => property.Name));
    }

    private static JsonElement WriteMaterials(out string text)
    {
        OutputFile file = TerrainMaterialsFile.Create(
            VirtualPath.Parse("/levels/test_level/art/terrains/main.materials.json"),
            Level,
            TerrainMaterialPresets.TextureSet("test_TerrainMaterialTextureSet"),
            [TerrainMaterialPresets.Grass("grass"), TerrainMaterialPresets.Asphalt("asphalt")]);
        text = file.Text();
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
