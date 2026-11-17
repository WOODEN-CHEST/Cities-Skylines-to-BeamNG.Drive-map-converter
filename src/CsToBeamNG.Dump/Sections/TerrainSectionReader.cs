#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>terrain</c> section written by <see cref="TerrainSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class TerrainSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="TerrainSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">A file is missing, damaged or inconsistent with <c>terrain.json</c>.</exception>
    public static TerrainSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string infoPath = section.GetFilePath(TerrainSection.InfoFileName);
        using JsonDocument document = section.ReadJson(TerrainSection.InfoFileName);
        var json = new JsonFieldReader(infoPath);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        GridGeometry grid = GridGeometry.Read(json, root, "grid", "$");
        JsonElement detail = json.RequiredObject(root, "detail", "$");
        GridGeometry detailGrid = GridGeometry.Read(json, detail, "grid", "$.detail");
        int tileResolution = json.RequiredInt32(detail, "tileResolution", "$.detail", 2, GridGeometry.MaxResolution);
        if (((long)(tileResolution - 1) * TileCoordinate.GridSize) + 1 != detailGrid.Resolution)
        {
            throw json.Error(
                "$.detail.tileResolution",
                string.Create(CultureInfo.InvariantCulture, $"{TileCoordinate.GridSize} tiles of {tileResolution} points don't span a detail grid of {detailGrid.Resolution} points"));
        }

        var tiles = new List<TileCoordinate>();
        foreach ((JsonElement item, string path) in json.RequiredArray(detail, "tiles", "$.detail"))
        {
            json.RequireObject(item, path);
            var tile = new TileCoordinate(
                json.RequiredInt32(item, "x", path, 0, TileCoordinate.GridSize - 1),
                json.RequiredInt32(item, "z", path, 0, TileCoordinate.GridSize - 1));
            if (tiles.Contains(tile))
            {
                throw json.Error(path, "tile " + tile + " is listed twice");
            }

            tiles.Add(tile);
        }

        ushort[] raw = ReadHeights(section, TerrainSection.RawHeightsFileName, grid.PointCount);
        ushort[] final = ReadHeights(section, TerrainSection.FinalHeightsFileName, grid.PointCount);
        int tilePoints = tileResolution * tileResolution;
        ushort[] detailHeights = ReadHeights(section, TerrainSection.DetailHeightsFileName, (long)tilePoints * tiles.Count);
        var content = new TerrainSectionContent(grid, raw, final, detailGrid, tileResolution);
        for (int i = 0; i < tiles.Count; i++)
        {
            content.DetailTiles.Add(new TerrainDetailTile(tiles[i], detailHeights.AsSpan(i * tilePoints, tilePoints).ToArray()));
        }

        return content;
    }

    private static ushort[] ReadHeights(DumpSection section, string path, long expectedCount)
    {
        using RecordFileReader records = section.OpenRecordFile(path, TerrainSection.HeightRecordType, TerrainSection.HeightRecordSize);
        if (records.RecordCount != expectedCount)
        {
            throw new DumpException(
                DumpErrorKind.Corrupt,
                string.Create(CultureInfo.InvariantCulture, $"'{records.SourceName}' has {records.RecordCount} heights, but terrain.json describes {expectedCount}."));
        }

        return records.ReadAllRecords<ushort>();
    }
}
#endif
