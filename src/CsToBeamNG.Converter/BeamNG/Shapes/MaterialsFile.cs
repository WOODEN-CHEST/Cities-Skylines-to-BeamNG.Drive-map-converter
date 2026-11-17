using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>
/// A <c>*.materials.json</c> file with <see cref="ShapeMaterial"/>s. The game loads every such file below the level
/// folder when the level loads (docs/research/beamng-level-format.md, Materials).
/// </summary>
public static class MaterialsFile
{
    /// <summary>The material version this writer produces: the physically based one.</summary>
    public const float MaterialVersion = 1.5f;

    // A material has four stages (layers); unused ones are empty objects, as in the game's own files.
    private const int StageCount = 4;

    /// <summary>Creates the file.</summary>
    /// <param name="path">Where the file goes; its name must end with <c>materials.json</c>.</param>
    /// <param name="levelName">The level's folder name, which seeds the persistent ids.</param>
    /// <param name="materials">The materials, with unique names.</param>
    /// <returns>The file.</returns>
    public static OutputFile Create(VirtualPath path, string levelName, IReadOnlyList<ShapeMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(materials);
        BeamNGNames.ThrowIfNotGeneratedName(levelName, nameof(levelName));
        if (!path.Name.EndsWith("materials.json", StringComparison.Ordinal))
        {
            throw new ArgumentException($"The game only loads files named *materials.json, not '{path.Name}'.", nameof(path));
        }

        // Keys in ordinal order, as in the game's own files.
        var entries = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (ShapeMaterial material in materials)
        {
            if (!entries.TryAdd(material.Name, ToJson(levelName, material)))
            {
                throw new ArgumentException($"The material '{material.Name}' is defined twice.", nameof(materials));
            }
        }

        var document = new JsonObject();
        foreach (KeyValuePair<string, JsonNode> entry in entries)
        {
            document.Add(entry.Key, entry.Value);
        }

        return new OutputFile(path, stream => BeamNGJson.WriteDocument(stream, document));
    }

    private static JsonObject ToJson(string levelName, ShapeMaterial material)
    {
        var stage = new JsonObject();
        if (material.BaseColorFactor is Vector4 baseColor)
        {
            stage.Add("baseColorFactor", BeamNGJson.ToJson(baseColor));
        }

        if (material.MetallicFactor is float metallic)
        {
            stage.Add("metallicFactor", BeamNGJson.ToJson(metallic));
        }

        if (material.RoughnessFactor is float roughness)
        {
            stage.Add("roughnessFactor", BeamNGJson.ToJson(roughness));
        }

        var stages = new JsonArray { stage };
        for (int i = 1; i < StageCount; i++)
        {
            stages.Add(new JsonObject());
        }

        // The leading fields come first, in the order of the game's own files.
        return new JsonObject
        {
            ["name"] = material.Name,
            ["mapTo"] = material.Name,
            ["class"] = "Material",
            ["persistentId"] = PersistentId.ForObject(levelName, "material:" + material.Name),
            ["Stages"] = stages,
            ["version"] = BeamNGJson.ToJson(MaterialVersion),
        };
    }
}
