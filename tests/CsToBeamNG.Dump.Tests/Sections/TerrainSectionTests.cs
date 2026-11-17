using System;
using System.IO;
using System.Linq;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Dump.Tests.Sections;

public sealed class TerrainSectionTests : IDisposable
{
    // A small terrain with the game's layout: 9 × 9 tiles, each 4 detail cells wide; the main grid has 2 points per tile.
    private const int DetailTileResolution = 5;

    private static readonly GridGeometry _grid = new(-72, -72, 8, 19);
    private static readonly GridGeometry _detailGrid = new(-72, -72, 4, ((DetailTileResolution - 1) * 9) + 1);

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsATerrain(string culture)
    {
        using var scope = new CultureScope(culture);
        TerrainSectionContent written = CreateContent(new TileCoordinate(4, 4), new TileCoordinate(3, 4), new TileCoordinate(4, 3));
        DumpSection section = WriteAndOpen(written);

        TerrainSectionContent read = TerrainSectionReader.Read(section);

        Assert.Equal(TerrainSection.Version, section.Version);
        Assert.Equal(written.RawHeights, read.RawHeights);
        Assert.Equal(written.FinalHeights, read.FinalHeights);
        Assert.Equal(DetailTileResolution, read.DetailTileResolution);
        AssertGrid(_grid, read.Grid);
        AssertGrid(_detailGrid, read.DetailGrid);
        Assert.Equal([new TileCoordinate(4, 3), new TileCoordinate(3, 4), new TileCoordinate(4, 4)], read.DetailTiles.Select(t => t.Tile));
        foreach (TerrainDetailTile tile in read.DetailTiles)
        {
            Assert.Equal(written.DetailTiles.Single(t => t.Tile == tile.Tile).Heights, tile.Heights);
        }
    }

    [Fact]
    public void WritesTheDocumentedFiles()
    {
        using var scope = new CultureScope("lv-LV");
        DumpSection section = WriteAndOpen(CreateContent(new TileCoordinate(8, 0)));

        Assert.Equal(
            [TerrainSection.InfoFileName, TerrainSection.RawHeightsFileName, TerrainSection.FinalHeightsFileName, TerrainSection.DetailHeightsFileName],
            section.Files.Select(f => f.Path));
        Assert.Null(section.RecordCount);
        Assert.Equal(
            """
            {
              "grid": {
                "originX": -72,
                "originZ": -72,
                "spacing": 8,
                "resolution": 19
              },
              "detail": {
                "grid": {
                  "originX": -72,
                  "originZ": -72,
                  "spacing": 4,
                  "resolution": 37
                },
                "tileResolution": 5,
                "tiles": [
                  {
                    "x": 8,
                    "z": 0
                  }
                ]
              }
            }

            """,
            File.ReadAllText(section.GetFilePath(TerrainSection.InfoFileName)));
        using RecordFileReader records = section.OpenRecordFile(TerrainSection.DetailHeightsFileName, TerrainSection.HeightRecordType, TerrainSection.HeightRecordSize);
        Assert.Equal(DetailTileResolution * DetailTileResolution, records.RecordCount);
        Assert.Equal(2, records.RecordSize);
    }

    [Fact]
    public void WritesATerrainWithoutDetailTiles()
    {
        TerrainSectionContent read = TerrainSectionReader.Read(WriteAndOpen(CreateContent()));

        Assert.Empty(read.DetailTiles);
    }

    [Fact]
    public void RejectsHeightsThatDontFillTheGrid()
    {
        var content = new TerrainSectionContent(_grid, new ushort[_grid.PointCount - 1], new ushort[_grid.PointCount], _detailGrid, DetailTileResolution);

        ArgumentException error = Assert.Throws<ArgumentException>(() => Write(content));

        Assert.Contains("raw heights have 360 values, but the grid has 361 points", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInconsistentDetailTiles()
    {
        TerrainSectionContent duplicate = CreateContent(new TileCoordinate(1, 2), new TileCoordinate(1, 2));
        Assert.Contains("listed twice", Assert.Throws<ArgumentException>(() => Write(duplicate)).Message, StringComparison.Ordinal);

        TerrainSectionContent shortTile = CreateContent();
        shortTile.DetailTiles.Add(new TerrainDetailTile(new TileCoordinate(0, 0), new ushort[3]));
        Assert.Contains("detail heights of tile (0, 0)", Assert.Throws<ArgumentException>(() => Write(shortTile)).Message, StringComparison.Ordinal);

        var mismatch = new TerrainSectionContent(_grid, new ushort[_grid.PointCount], new ushort[_grid.PointCount], _detailGrid, 6);
        Assert.Contains("doesn't fit a detail grid", Assert.Throws<ArgumentException>(() => Write(mismatch)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidGrids()
    {
        Assert.Throws<ArgumentException>(() => new GridGeometry(float.NaN, 0, 16, 10));
        Assert.Throws<ArgumentException>(() => new GridGeometry(0, 0, 0, 10));
        Assert.Throws<ArgumentException>(() => new GridGeometry(0, 0, 16, 1));
        Assert.Throws<ArgumentException>(() => new GridGeometry(0, 0, 16, GridGeometry.MaxResolution + 1));
        Assert.Equal("1081 × 1081 points, 16 m apart, from (-8640, -8640)", new GridGeometry(-8640, -8640, 16, 1081).ToString());
    }

    [Fact]
    public void ReportsHeightFilesThatDontMatchTheInfoFile()
    {
        string folder = WriteRawSection(resolution: 2, rawHeights: 5, spacing: "8");

        DumpException error = Assert.Throws<DumpException>(() => TerrainSectionReader.Read(Open(folder)));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("has 5 heights, but terrain.json describes 4", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAnInvalidInfoFile()
    {
        string folder = WriteRawSection(resolution: 2, rawHeights: 4, spacing: "0");

        DumpException error = Assert.Throws<DumpException>(() => TerrainSectionReader.Read(Open(folder)));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("$.grid", error.Message, StringComparison.Ordinal);
        Assert.Contains("spacing must be a finite number of metres above 0", error.Message, StringComparison.Ordinal);
    }

    private static TerrainSectionContent CreateContent(params TileCoordinate[] detailTiles)
    {
        var random = new Random(17);
        ushort[] raw = new ushort[_grid.PointCount];
        ushort[] final = new ushort[_grid.PointCount];
        for (int i = 0; i < raw.Length; i++)
        {
            raw[i] = (ushort)random.Next(0, 65536);
            final[i] = (ushort)(i % 3 == 0 ? raw[i] : random.Next(0, 65536));
        }

        raw[0] = ushort.MaxValue;
        final[1] = 0;
        var content = new TerrainSectionContent(_grid, raw, final, _detailGrid, DetailTileResolution);
        foreach (TileCoordinate tile in detailTiles)
        {
            ushort[] heights = new ushort[DetailTileResolution * DetailTileResolution];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = (ushort)((tile.X * 1000) + (tile.Z * 100) + i);
            }

            content.DetailTiles.Add(new TerrainDetailTile(tile, heights));
        }

        return content;
    }

    private static void AssertGrid(GridGeometry expected, GridGeometry actual)
    {
        Assert.Equal(expected.OriginX, actual.OriginX);
        Assert.Equal(expected.OriginZ, actual.OriginZ);
        Assert.Equal(expected.Spacing, actual.Spacing);
        Assert.Equal(expected.Resolution, actual.Resolution);
    }

    private static DumpSection Open(string folder) => DumpReader.Open(folder).FindSection(TerrainSection.Name)!;

    private DumpSection WriteAndOpen(TerrainSectionContent content) => Open(Write(content));

    private string Write(TerrainSectionContent content)
    {
        DumpWriter dump = DumpWriter.Create(_temp.PathOf("dump"), TestManifests.Minimal());
        TerrainSectionWriter.Write(dump, content);
        dump.Manifest.Complete = true;
        dump.WriteManifest();
        return dump.Folder;
    }

    // Writes a terrain section by hand, without the writer's checks, to test what the reader does with bad data.
    private string WriteRawSection(int resolution, int rawHeights, string spacing)
    {
        DumpWriter dump = DumpWriter.Create(_temp.PathOf("raw"), TestManifests.Minimal());
        SectionWriter section = dump.BeginSection(TerrainSection.Name, TerrainSection.Version);
        using (JsonWriter json = section.CreateJsonFile(TerrainSection.InfoFileName))
        {
            json.WriteStartObject();
            WriteGrid(json, "grid", spacing, resolution);
            json.WriteStartObject("detail");
            WriteGrid(json, "grid", "4", 10);
            json.WriteNumber("tileResolution", 2);
            json.WriteStartArray("tiles");
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        WriteHeights(section, TerrainSection.RawHeightsFileName, rawHeights);
        WriteHeights(section, TerrainSection.FinalHeightsFileName, resolution * resolution);
        WriteHeights(section, TerrainSection.DetailHeightsFileName, 0);
        section.Complete();
        dump.Manifest.Complete = true;
        dump.WriteManifest();
        return dump.Folder;
    }

    private static void WriteGrid(JsonWriter json, string name, string spacing, int resolution)
    {
        json.WriteStartObject(name);
        json.WriteNumber("originX", 0);
        json.WriteNumber("originZ", 0);
        json.WriteNumber("spacing", float.Parse(spacing, System.Globalization.CultureInfo.InvariantCulture));
        json.WriteNumber("resolution", resolution);
        json.WriteEndObject();
    }

    private static void WriteHeights(SectionWriter section, string path, int count)
    {
        using RecordFileWriter records = section.CreateRecordFile(path, TerrainSection.HeightRecordType, TerrainSection.HeightRecordSize);
        for (int i = 0; i < count; i++)
        {
            records.BeginRecord();
            records.WriteUInt16((ushort)i);
            records.EndRecord();
        }

        records.Finish();
    }
}
