using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes a <c>terrain</c> section (docs/dump-format.md, <c>terrain</c>).</summary>
public static class TerrainSectionWriter
{
    /// <summary>
    /// Writes the section and completes it, so it is added to the dump's manifest. Detail tiles are written ordered
    /// by z, then x, whatever their order in <paramref name="content"/>.
    /// </summary>
    /// <param name="dump">The dump being written.</param>
    /// <param name="content">The terrain.</param>
    /// <exception cref="ArgumentException">The content is inconsistent, such as an array of the wrong length.</exception>
    public static void Write(DumpWriter dump, TerrainSectionContent content)
    {
        Guard.NotNull(dump, nameof(dump));
        List<TerrainDetailTile> tiles = Validate(content);
        SectionWriter section = dump.BeginSection(TerrainSection.Name, TerrainSection.Version);
        using (JsonWriter json = section.CreateJsonFile(TerrainSection.InfoFileName))
        {
            json.WriteStartObject();
            content.Grid.Write(json, "grid");
            json.WriteStartObject("detail");
            content.DetailGrid.Write(json, "grid");
            json.WriteNumber("tileResolution", content.DetailTileResolution);
            json.WriteStartArray("tiles");
            foreach (TerrainDetailTile tile in tiles)
            {
                json.WriteStartObject();
                json.WriteNumber("x", tile.Tile.X);
                json.WriteNumber("z", tile.Tile.Z);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        WriteHeights(section, TerrainSection.RawHeightsFileName, [content.RawHeights]);
        WriteHeights(section, TerrainSection.FinalHeightsFileName, [content.FinalHeights]);
        var detailHeights = new List<ushort[]>(tiles.Count);
        foreach (TerrainDetailTile tile in tiles)
        {
            detailHeights.Add(tile.Heights);
        }

        WriteHeights(section, TerrainSection.DetailHeightsFileName, detailHeights);
        section.Complete();
    }

    private static void WriteHeights(SectionWriter section, string path, IList<ushort[]> blocks)
    {
        using RecordFileWriter records = section.CreateRecordFile(path, TerrainSection.HeightRecordType, TerrainSection.HeightRecordSize);
        foreach (ushort[] heights in blocks)
        {
            foreach (ushort height in heights)
            {
                records.BeginRecord();
                records.WriteUInt16(height);
                records.EndRecord();
            }
        }

        records.Finish();
    }

    private static List<TerrainDetailTile> Validate(TerrainSectionContent content)
    {
        Guard.NotNull(content, nameof(content));
        CheckLength("raw heights", content.RawHeights.Length, content.Grid.PointCount);
        CheckLength("final heights", content.FinalHeights.Length, content.Grid.PointCount);
        int tileResolution = content.DetailTileResolution;
        if (tileResolution < 2 || ((long)(tileResolution - 1) * TileCoordinate.GridSize) + 1 != content.DetailGrid.Resolution)
        {
            throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                "A detail tile resolution of {0} doesn't fit a detail grid of resolution {1}: {2} tiles of (resolution − 1) cells must span the grid.",
                tileResolution,
                content.DetailGrid.Resolution,
                TileCoordinate.GridSize));
        }

        var tiles = new List<TerrainDetailTile>(content.DetailTiles);
        tiles.Sort((a, b) => a.Tile.Z != b.Tile.Z ? a.Tile.Z.CompareTo(b.Tile.Z) : a.Tile.X.CompareTo(b.Tile.X));
        for (int i = 0; i < tiles.Count; i++)
        {
            Guard.NotNull(tiles[i], nameof(content));
            if (i > 0 && tiles[i].Tile == tiles[i - 1].Tile)
            {
                throw new ArgumentException("Detail tile " + tiles[i].Tile + " is listed twice.", nameof(content));
            }

            CheckLength("detail heights of tile " + tiles[i].Tile, tiles[i].Heights.Length, tileResolution * tileResolution);
        }

        return tiles;
    }

    private static void CheckLength(string what, int length, int expected)
    {
        if (length != expected)
        {
            throw new ArgumentException(string.Format(
                CultureInfo.InvariantCulture,
                "The {0} have {1} values, but the grid has {2} points.",
                what,
                length,
                expected));
        }
    }
}
