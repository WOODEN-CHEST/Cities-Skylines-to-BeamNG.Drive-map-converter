using System;
using System.Collections.Generic;
using System.Linq;
using CsToBeamNG.Converter.BeamNG.Terrain;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class TerrainMaterialPresetsTests
{
    public static TheoryData<string, string> Presets => new()
    {
        { "grass", "GRASS" },
        { "dirt", "DIRT" },
        { "rock", "ROCK" },
        { "sand", "SAND" },
        { "asphalt", "ASPHALT" },
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void ReferenceOnlyTexturesThatShipWithTheGame(string name, string groundModel)
    {
        TerrainMaterial material = Create(name);

        Assert.Equal(name, material.InternalName);
        Assert.Equal(groundModel, material.GroundModelName);

        // The base layer: the template level's baked base maps, all five of them.
        foreach (TerrainMaterialMap? map in Maps(material.Base))
        {
            Assert.NotNull(map);
            Assert.StartsWith("/levels/template/art/terrains/t_terrain_base_", map.Texture.Value, StringComparison.Ordinal);
        }

        // The macro and detail layers: the shared terrain textures, or the template's own 1024 px textures.
        foreach (TerrainMaterialMap? map in Maps(material.Macro).Concat(Maps(material.Detail)))
        {
            Assert.NotNull(map);
            Assert.True(
                map.Texture.Value.StartsWith("/assets/materials/terrain/", StringComparison.Ordinal)
                || map.Texture.Value.StartsWith("/levels/template/art/terrains/t_", StringComparison.Ordinal),
                map.Texture.Value);
            Assert.EndsWith(".png", map.Texture.Value, StringComparison.Ordinal);
        }
    }

    private static TerrainMaterial Create(string name) => name switch
    {
        "grass" => TerrainMaterialPresets.Grass(name),
        "dirt" => TerrainMaterialPresets.Dirt(name),
        "rock" => TerrainMaterialPresets.Rock(name),
        "sand" => TerrainMaterialPresets.Sand(name),
        _ => TerrainMaterialPresets.Asphalt(name),
    };

    private static IEnumerable<TerrainMaterialMap?> Maps(TerrainMaterialLayer layer) =>
        [layer.BaseColor, layer.Normal, layer.Roughness, layer.AmbientOcclusion, layer.Height];
}
