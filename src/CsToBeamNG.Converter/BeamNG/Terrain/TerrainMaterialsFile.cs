using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// The terrain materials file, <c>art/terrains/main.materials.json</c>: the level's <see cref="TerrainMaterial"/>s
/// and its <see cref="TerrainMaterialTextureSet"/> (docs/research/beamng-level-format.md, Terrain materials).
/// </summary>
/// <remarks>
/// The game loads every <c>*materials.json</c> file below the level folder. As the game's editor does, each material
/// is named <c>&lt;internalName&gt;-&lt;persistentId&gt;</c>, which keeps names unique across levels.
/// </remarks>
public static class TerrainMaterialsFile
{
    /// <summary>Where official levels keep the file, relative to the level folder.</summary>
    public const string DefaultRelativePath = "art/terrains/main.materials.json";

    // The field-name prefix of each map, such as aoBaseTex for the base layer's ambient occlusion map.
    private static readonly (string Prefix, Func<TerrainMaterialLayer, TerrainMaterialMap?> Select)[] _maps =
    [
        ("ao", layer => layer.AmbientOcclusion),
        ("baseColor", layer => layer.BaseColor),
        ("height", layer => layer.Height),
        ("normal", layer => layer.Normal),
        ("roughness", layer => layer.Roughness),
    ];

    /// <summary>The name of a material's object: its internal name and its persistent id.</summary>
    /// <param name="levelName">The level's folder name.</param>
    /// <param name="material">The material.</param>
    /// <returns>The name, such as <c>grass-1b4e…</c>.</returns>
    public static string GetObjectName(string levelName, TerrainMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return material.InternalName + "-" + GetPersistentId(levelName, material);
    }

    /// <summary>Creates the file.</summary>
    /// <param name="path">Where the file goes, normally <see cref="DefaultRelativePath"/> in the level folder.</param>
    /// <param name="levelName">The level's folder name, which seeds the persistent ids.</param>
    /// <param name="textureSet">The texture set.</param>
    /// <param name="materials">The materials, at least one, with unique internal names.</param>
    /// <returns>The file.</returns>
    public static OutputFile Create(
        VirtualPath path, string levelName, TerrainMaterialTextureSet textureSet, IReadOnlyList<TerrainMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(textureSet);
        ArgumentNullException.ThrowIfNull(materials);
        var internalNames = new List<string>();
        foreach (TerrainMaterial material in materials)
        {
            internalNames.Add(material.InternalName);
        }

        TerrainData.ValidateMaterialNames(internalNames);

        // Keys in ordinal order, as in the game's own files.
        var entries = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (TerrainMaterial material in materials)
        {
            string name = GetObjectName(levelName, material);
            entries.Add(name, MaterialToJson(name, GetPersistentId(levelName, material), material));
        }

        if (!entries.TryAdd(textureSet.Name, TextureSetToJson(levelName, textureSet)))
        {
            throw new ArgumentException($"The texture set's name '{textureSet.Name}' is also a material name.", nameof(textureSet));
        }

        var document = new JsonObject();
        foreach (KeyValuePair<string, JsonNode> entry in entries)
        {
            document.Add(entry.Key, entry.Value);
        }

        return new OutputFile(path, stream => BeamNGJson.WriteDocument(stream, document));
    }

    private static string GetPersistentId(string levelName, TerrainMaterial material) =>
        PersistentId.ForObject(levelName, "terrain-material:" + material.InternalName);

    private static JsonObject MaterialToJson(string name, string persistentId, TerrainMaterial material)
    {
        var fields = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        if (material.Annotation is not null)
        {
            fields.Add("annotation", material.Annotation);
        }

        AddLayer(fields, "Base", material.Base);
        AddLayer(fields, "Macro", material.Macro);
        AddLayer(fields, "Detail", material.Detail);
        if (material.DetailDistances is Vector4 detailDistances)
        {
            fields.Add("detailDistances", BeamNGJson.ToJson(detailDistances));
        }

        fields.Add("groundmodelName", material.GroundModelName);
        if (material.MacroDistances is Vector4 macroDistances)
        {
            fields.Add("macroDistances", BeamNGJson.ToJson(macroDistances));
        }

        // The leading fields come first, in the order of the game's editor.
        var json = new JsonObject
        {
            ["name"] = name,
            ["internalName"] = material.InternalName,
            ["class"] = "TerrainMaterial",
            ["persistentId"] = persistentId,
        };
        foreach (KeyValuePair<string, JsonNode> field in fields)
        {
            json.Add(field.Key, field.Value);
        }

        return json;
    }

    private static void AddLayer(SortedDictionary<string, JsonNode> fields, string level, TerrainMaterialLayer layer)
    {
        foreach ((string prefix, Func<TerrainMaterialLayer, TerrainMaterialMap?> select) in _maps)
        {
            TerrainMaterialMap? map = select(layer);
            if (map is null)
            {
                continue;
            }

            string key = prefix + level;
            fields.Add(key + "Tex", map.Texture.Value);
            if (layer.TextureSize is float textureSize)
            {
                fields.Add(key + "TexSize", BeamNGJson.ToJson(textureSize));
            }

            if (map.Strength is Vector2 strength)
            {
                fields.Add(key + "Strength", BeamNGJson.ToJson(strength));
            }
        }
    }

    private static JsonObject TextureSetToJson(string levelName, TerrainMaterialTextureSet textureSet) => new()
    {
        ["name"] = textureSet.Name,
        ["class"] = "TerrainMaterialTextureSet",
        ["persistentId"] = PersistentId.ForObject(levelName, "terrain-texture-set:" + textureSet.Name),
        ["baseTexSize"] = new JsonArray(textureSet.BaseTexSize, textureSet.BaseTexSize),
        ["detailTexSize"] = new JsonArray(textureSet.DetailTexSize, textureSet.DetailTexSize),
        ["macroTexSize"] = new JsonArray(textureSet.MacroTexSize, textureSet.MacroTexSize),
    };
}
