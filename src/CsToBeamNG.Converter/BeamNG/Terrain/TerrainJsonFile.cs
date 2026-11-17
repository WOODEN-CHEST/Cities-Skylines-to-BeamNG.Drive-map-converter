using System;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// The <c>.terrain.json</c> file next to a <c>.ter</c> file. It describes the binary file for the game's editor;
/// the game loads the terrain from the <c>TerrainBlock</c>'s <c>terrainFile</c>, and an official level ships a
/// <c>.ter</c> without it (docs/research/beamng-level-format.md, Terrain).
/// </summary>
public static class TerrainJsonFile
{
    // The description the game's own files carry; it mentions a layerTextureMap that the files don't contain.
    private const string BinaryFormatDescription =
        "version(char), size(unsigned int), heightMap(heightMapSize * heightMapItemSize), layerMap(layerMapSize * layerMapItemSize), layerTextureMap(layerMapSize * layerMapItemSize), materialNames";

    /// <summary>Creates the file.</summary>
    /// <param name="path">Where the file goes, such as <c>/levels/x/theterrain.terrain.json</c>.</param>
    /// <param name="terFile">The <c>.ter</c> file it describes.</param>
    /// <param name="terrain">The terrain in that file.</param>
    /// <returns>The file.</returns>
    public static OutputFile Create(VirtualPath path, VirtualPath terFile, TerrainData terrain)
    {
        ArgumentNullException.ThrowIfNull(terFile);
        ArgumentNullException.ThrowIfNull(terrain);

        long samples = (long)terrain.Size * terrain.Size;
        var materials = new JsonArray();
        foreach (string name in terrain.MaterialNames)
        {
            materials.Add(name);
        }

        // Keys in ordinal order, as in the game's own files.
        var document = new JsonObject
        {
            ["binaryFormat"] = BinaryFormatDescription,
            ["datafile"] = terFile.Value,
            ["heightMapItemSize"] = sizeof(ushort),
            ["heightMapSize"] = samples,
            ["layerMapItemSize"] = sizeof(byte),
            ["layerMapSize"] = samples,
            ["materials"] = materials,
            ["size"] = terrain.Size,
            ["version"] = (int)TerFile.Version,
        };
        return new OutputFile(path, stream => BeamNGJson.WriteDocument(stream, document));
    }
}
