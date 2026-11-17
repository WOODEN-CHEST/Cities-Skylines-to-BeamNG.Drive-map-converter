using System;
using System.IO;
using System.Linq;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Records;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Dump.Tests.Sections;

public sealed class WaterSectionTests : IDisposable
{
    private static readonly GridGeometry _grid = new(-24, -24, 16, 4);

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsTheWater(string culture)
    {
        using var scope = new CultureScope(culture);
        WaterSectionContent written = CreateContent();
        DumpSection section = WriteAndOpen(written);

        WaterSectionContent read = WaterSectionReader.Read(section);

        Assert.Equal(WaterSection.Version, section.Version);
        Assert.Equal(59.999004f, read.SeaLevel);
        Assert.Equal(_grid.Resolution, read.Grid.Resolution);
        Assert.Equal(_grid.Spacing, read.Grid.Spacing);
        Assert.Equal(_grid.OriginX, read.Grid.OriginX);
        Assert.Equal(written.Ground, read.Ground);
        Assert.Equal(written.Depth, read.Depth);
        Assert.Equal(written.Pollution, read.Pollution);
        Assert.Equal(written.VelocityX, read.VelocityX);
        Assert.Equal(written.VelocityZ, read.VelocityZ);
        Assert.Equal([2u, 7u], read.Sources.Select(s => s.Id));
        WaterSourceRecord source = read.Sources[1];
        Assert.Equal(WaterSourceRecord.FacilityType, source.Type);
        Assert.Equal(3200, source.Target);
        Assert.Equal((-100.5f, 61.25f, 2000.125f), (source.InputX, source.InputY, source.InputZ));
        Assert.Equal((-110.5f, 62.25f, 2010.125f), (source.OutputX, source.OutputY, source.OutputZ));
        Assert.Equal((1u, 2u, 3u, 4u, uint.MaxValue), (source.Water, source.Pollution, source.InputRate, source.OutputRate, source.Flow));
    }

    [Fact]
    public void WritesTheDocumentedFiles()
    {
        using var scope = new CultureScope("lv-LV");
        DumpSection section = WriteAndOpen(CreateContent());

        Assert.Equal([WaterSection.InfoFileName, WaterSection.CellsFileName, WaterSection.SourcesFileName], section.Files.Select(f => f.Path));
        Assert.Equal(2, section.RecordCount);
        Assert.Equal(
            """
            {
              "seaLevel": 59.999004,
              "grid": {
                "originX": -24,
                "originZ": -24,
                "spacing": 16,
                "resolution": 4
              }
            }

            """,
            File.ReadAllText(section.GetFilePath(WaterSection.InfoFileName)));
        Assert.Equal(64 + (16 * 10), new FileInfo(section.GetFilePath(WaterSection.CellsFileName)).Length);
        Assert.Equal(64 + (2 * 52), new FileInfo(section.GetFilePath(WaterSection.SourcesFileName)).Length);
    }

    [Fact]
    public void RejectsInconsistentContent()
    {
        var shortDepth = new WaterSectionContent(40, _grid, new ushort[16], new ushort[15], new ushort[16], new short[16], new short[16]);
        Assert.Contains("15 depths, but the grid has 16 points", Assert.Throws<ArgumentException>(() => Write(shortDepth)).Message, StringComparison.Ordinal);

        var noSeaLevel = new WaterSectionContent(float.NaN, _grid, new ushort[16], new ushort[16], new ushort[16], new short[16], new short[16]);
        Assert.Contains("sea level", Assert.Throws<ArgumentException>(() => Write(noSeaLevel)).Message, StringComparison.Ordinal);

        WaterSectionContent duplicate = CreateContent();
        duplicate.Sources.Add(new WaterSourceRecord { Id = 7 });
        Assert.Contains("Water source 7 is listed twice", Assert.Throws<ArgumentException>(() => Write(duplicate)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsCellsThatDontMatchTheInfoFile()
    {
        DumpWriter dump = DumpWriter.Create(_temp.PathOf("raw"), TestManifests.Minimal());
        SectionWriter section = dump.BeginSection(WaterSection.Name, WaterSection.Version);
        using (JsonWriter json = section.CreateJsonFile(WaterSection.InfoFileName))
        {
            json.WriteStartObject();
            json.WriteNumber("seaLevel", 40f);
            json.WriteStartObject("grid");
            json.WriteNumber("originX", 0);
            json.WriteNumber("originZ", 0);
            json.WriteNumber("spacing", 16);
            json.WriteNumber("resolution", 2);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        using (RecordFileWriter cells = section.CreateRecordFile(WaterSection.CellsFileName, WaterSection.CellRecordType, WaterSection.CellRecordSize))
        {
            cells.BeginRecord();
            cells.WriteZeros(WaterSection.CellRecordSize);
            cells.EndRecord();
            cells.Finish();
        }

        using (RecordFileWriter sources = section.CreateRecordFile(WaterSection.SourcesFileName, WaterSection.SourceRecordType, WaterSection.SourceRecordSize))
        {
            sources.Finish();
        }

        section.Complete();
        dump.Manifest.Complete = true;
        dump.WriteManifest();

        DumpException error = Assert.Throws<DumpException>(() => WaterSectionReader.Read(DumpReader.Open(dump.Folder).FindSection(WaterSection.Name)!));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("has 1 cells, but water.json describes 4", error.Message, StringComparison.Ordinal);
    }

    private static WaterSectionContent CreateContent()
    {
        int points = _grid.PointCount;
        var content = new WaterSectionContent(59.999004f, _grid, new ushort[points], new ushort[points], new ushort[points], new short[points], new short[points]);
        for (int i = 0; i < points; i++)
        {
            content.Ground[i] = (ushort)(3000 + (i * 7));
            content.Depth[i] = (ushort)(i % 4 == 0 ? 0 : i * 100);
            content.Pollution[i] = (ushort)(i * 3);
            content.VelocityX[i] = (short)(-i * 50);
            content.VelocityZ[i] = (short)(i == 5 ? short.MinValue : i);
        }

        content.Sources.Add(new WaterSourceRecord
        {
            Id = 7,
            Type = WaterSourceRecord.FacilityType,
            Target = 3200,
            InputX = -100.5f,
            InputY = 61.25f,
            InputZ = 2000.125f,
            OutputX = -110.5f,
            OutputY = 62.25f,
            OutputZ = 2010.125f,
            Water = 1,
            Pollution = 2,
            InputRate = 3,
            OutputRate = 4,
            Flow = uint.MaxValue,
        });
        content.Sources.Add(new WaterSourceRecord { Id = 2, Type = WaterSourceRecord.NaturalType, Target = 3840 });
        return content;
    }

    private DumpSection WriteAndOpen(WaterSectionContent content) =>
        DumpReader.Open(Write(content)).FindSection(WaterSection.Name)!;

    private string Write(WaterSectionContent content)
    {
        DumpWriter dump = DumpWriter.Create(_temp.PathOf("dump"), TestManifests.Minimal());
        WaterSectionWriter.Write(dump, content);
        dump.Manifest.Complete = true;
        dump.WriteManifest();
        return dump.Folder;
    }
}
