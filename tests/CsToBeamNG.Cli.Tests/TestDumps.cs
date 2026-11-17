using System;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Cli.Tests;

/// <summary>
/// Writes small synthetic dumps with the real dump writer: a manifest and, optionally, a section no loader knows, or a
/// coarse terrain and water.
/// </summary>
internal static class TestDumps
{
    public static DumpManifest Manifest()
    {
        var manifest = new DumpManifest
        {
            CreatedUtc = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc),
            Exporter = new ExporterInfo("CsToBeamNG.Exporter", "0.1.0-test"),
            Game = new GameInfo { Version = "1.21.1-f9" },
            City = new CityInfo { Name = "Synthetic City", MapName = "Test Map", Environment = "Sunny" },
        };
        manifest.City.UnlockedTiles.Add(new TileCoordinate(4, 4));
        manifest.Counts["buildings"] = 12;
        manifest.Counts["trees"] = 183346;
        return manifest;
    }

    /// <summary>Writes a dump.</summary>
    /// <param name="folder">A new or empty folder.</param>
    /// <param name="manifest">The manifest, or <see langword="null"/> for <see cref="Manifest"/>.</param>
    /// <param name="complete">Whether the export finished.</param>
    /// <param name="unknownSection">The name of a section with one small JSON file, or <see langword="null"/> for none.</param>
    /// <returns>The dump folder's full path.</returns>
    /// <param name="terrainAndWater">Whether to add a coarse terrain and water (<see cref="AddTerrainAndWater"/>).</param>
    public static string Write(string folder, DumpManifest? manifest = null, bool complete = true, string? unknownSection = null, bool terrainAndWater = false)
    {
        manifest ??= Manifest();
        manifest.Complete = false;
        DumpWriter writer = DumpWriter.Create(folder, manifest);
        writer.WriteManifest();
        if (terrainAndWater)
        {
            AddTerrainAndWater(writer);
        }

        if (unknownSection is not null)
        {
            SectionWriter section = writer.BeginSection(unknownSection, new DumpVersion(0, 3));
            using (JsonWriter json = section.CreateJsonFile("data.json"))
            {
                json.WriteStartObject();
                json.WriteEndObject();
            }

            section.RecordCount = 1;
            section.Complete();
        }

        manifest.Complete = complete;
        writer.WriteManifest();
        return writer.Folder;
    }

    /// <summary>
    /// Adds a terrain of 10 × 10 heights 1920 m apart across the map, rising 1 m per 64 m towards the east, with detail
    /// heights 2 m higher in tile (4, 4); and water 3 m deep over the western half of the map, with one natural source.
    /// </summary>
    private static void AddTerrainAndWater(DumpWriter writer)
    {
        var grid = new GridGeometry(-8640, -8640, 1920, 10);
        ushort[] heights = new ushort[grid.PointCount];
        ushort[] depth = new ushort[grid.PointCount];
        for (int j = 0; j < grid.Resolution; j++)
        {
            for (int i = 0; i < grid.Resolution; i++)
            {
                heights[(j * grid.Resolution) + i] = (ushort)(6400 + (i * 1920));
                depth[(j * grid.Resolution) + i] = (ushort)(i < 5 ? 192 : 0);
            }
        }

        var terrain = new TerrainSectionContent(grid, heights, heights, new GridGeometry(-8640, -8640, 960, 19), 3);
        ushort[] detail = new ushort[9];
        for (int k = 0; k < detail.Length; k++)
        {
            detail[k] = (ushort)(6400 + 128 + ((4 * 1920) + ((k % 3) * 960)));
        }

        terrain.DetailTiles.Add(new TerrainDetailTile(new TileCoordinate(4, 4), detail));
        TerrainSectionWriter.Write(writer, terrain);

        int points = grid.PointCount;
        var water = new WaterSectionContent(40, grid, heights, depth, new ushort[points], new short[points], new short[points]);
        water.Sources.Add(new WaterSourceRecord { Id = 1, Type = WaterSourceRecord.NaturalType, Target = 6400, InputX = -5000, InputZ = 100, OutputX = -5000, OutputZ = 100 });
        WaterSectionWriter.Write(writer, water);
    }
}
