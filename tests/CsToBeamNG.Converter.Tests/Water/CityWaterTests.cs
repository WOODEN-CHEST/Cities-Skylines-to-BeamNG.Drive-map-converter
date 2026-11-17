using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Water;

public sealed class CityWaterTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SamplesTheDepthBilinearly()
    {
        // Water 2 m deep west of x = 0, dry from x = 8 m on; the ground rises with z.
        var water = new CityWater(TestTerrain.CreateWater(40, (x, z) => 30 + (z / 8), (x, z) => x <= 0 ? 2 : 0), []);

        Assert.Equal(2, water.SampleDepth(-20, 5), 12);
        Assert.Equal(1, water.SampleDepth(4, 5), 12);
        Assert.Equal(0, water.SampleDepth(30, 5), 12);
        Assert.Equal(2, water.SampleDepth(-1e6, 1e6), 12);
        Assert.Equal(30 + 2 + (16.0 / 8), water.SurfaceAt(7, 11));
        Assert.Null(water.SurfaceAt(10, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => water.SurfaceAt(19, 0));
    }

    [Fact]
    public void ReportsStatistics()
    {
        using var scope = new CultureScope("lv-LV");
        WaterSourceRecord[] sources =
        [
            new() { Id = 1, Type = WaterSourceRecord.NaturalType },
            new() { Id = 2, Type = WaterSourceRecord.FacilityType },
            new() { Id = 3, Type = WaterSourceRecord.FacilityType },
            new() { Id = 9, Type = 7 },
        ];
        var water = new CityWater(TestTerrain.CreateWater(59.999004f, (x, z) => 40, (x, z) => x < 0 ? 2.5 : x == 0 ? 0.25 : 0), sources);

        Assert.Equal(
            [
                ("sea level", "59.999 m"),
                ("grid", "19 × 19 points, 8 m apart, from (-72, -72)"),
                ("cells with water", "190 of 361 (0.01 km²), up to 2.5 m deep"),
                ("water surface", "40.25 to 42.5 m"),
                ("water sources", "4: 1 natural, 2 of buildings, 0 of water treatment, 1 of unknown types"),
            ],
            water.Statistics.Select(s => (s.Name, s.Value)));
        Assert.Equal(190, water.WetCells);
        Assert.Null(water.AreaOfInterestBounds);
    }

    [Fact]
    public void LoadsTheWaterAndSkipsSourcesWithoutAPosition()
    {
        WaterSectionContent content = TestTerrain.CreateWater(45, (x, z) => 40, (x, z) => 1);
        content.Sources.Add(new WaterSourceRecord { Id = 4, Type = WaterSourceRecord.NaturalType, OutputX = 3, OutputZ = 4 });
        content.Sources.Add(new WaterSourceRecord { Id = 5, Type = WaterSourceRecord.NaturalType, InputY = float.NaN });
        string folder = new SyntheticDump().WithWater(content).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Equal(SectionLoadStatus.Loaded, Assert.Single(result.Sections).Status);
        CityWater? water = result.World.Find<CityWater>();
        Assert.NotNull(water);
        Assert.Equal(45, water.SeaLevel);
        Assert.Equal(4u, Assert.Single(water.Sources).Id);
        LoadMessage warning = Assert.Single(result.Messages);
        Assert.Equal(new LoadMessage(LoadMessageSeverity.Warning, "Water source 5 has a position that isn't finite, so it is skipped.", "water"), warning);
        Assert.Equal(["terrain", "net-prefabs", "net-nodes", "net-segments", "net-lanes"], result.MissingSections);
    }
}
