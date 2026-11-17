using System;
using System.IO;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class TerrainLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LoadsTheTerrainWithTheDefaultLoaders()
    {
        TerrainSectionContent content = TestTerrain.Create((x, z) => 20 + (x / 8), (x, z) => 21 + (z / 8))
            .WithDetail(new TileCoordinate(4, 4), (x, z) => 30);
        string folder = new SyntheticDump().WithTerrain(content).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        SectionLoadResult section = Assert.Single(result.Sections);
        Assert.Equal(SectionLoadStatus.Loaded, section.Status);
        Assert.Empty(result.Messages);
        CityTerrain? terrain = result.World.Find<CityTerrain>();
        Assert.NotNull(terrain);
        Assert.Equal(20 + (16.0 / 8), terrain.RawHeights.Sample(16, 3), 9);
        Assert.Equal(21 + (24.0 / 8), terrain.FinalHeights.Sample(-5, 24), 9);
        Assert.Equal(30, terrain.SampleGround(0, 0), 9);
        Assert.Equal(["water"], result.MissingSections);
    }

    [Fact]
    public void NotesATerrainWithoutDetailHeights()
    {
        string folder = new SyntheticDump().WithTerrain(TestTerrain.Create((x, z) => 1, (x, z) => 1)).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        LoadMessage message = Assert.Single(result.Messages);
        Assert.Equal(LoadMessageSeverity.Info, message.Severity);
        Assert.Equal("terrain", message.SectionName);
        Assert.Contains("no 4 m detail heights", message.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsADamagedTerrainWithAnError()
    {
        string folder = new SyntheticDump().WithTerrain(TestTerrain.Create((x, z) => 1, (x, z) => 1)).WriteTo(_temp.PathOf("dump"));
        string heights = Path.Join(folder, TerrainSection.Name, TerrainSection.RawHeightsFileName);
        byte[] bytes = File.ReadAllBytes(heights);
        bytes[0] = (byte)'X';
        File.WriteAllBytes(heights, bytes);

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Equal(SectionLoadStatus.Failed, Assert.Single(result.Sections).Status);
        Assert.True(result.HasErrors);
        Assert.Null(result.World.Find<CityTerrain>());
        Assert.Contains(result.Messages, m => m.Severity == LoadMessageSeverity.Error && m.Message.Contains(TerrainSection.RawHeightsFileName, StringComparison.Ordinal));
    }

    [Fact]
    public void IsRegisteredBeforeTheWater() =>
        Assert.Equal(["terrain", "water"], SectionLoaderRegistry.CreateDefault().Loaders.Select(l => l.SectionName).Take(2));
}
