using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Planning;

public sealed class AreaOfInterestTests
{
    [Fact]
    public void CoversTheContentWithAMargin()
    {
        CityWorld world = World([new(4, 4)], new TestPointsData([new(0, 50, 0), new(100, 20, -200)]));

        AreaOfInterest area = AreaOfInterest.Plan(world);

        Assert.Equal(new MapRect(-500, -700, 600, 500), area.Bounds);
        Assert.Equal(AreaOfInterestSource.Content, area.Source);
        Assert.Equal(new MapRect(0, -200, 100, 0), area.ContentBounds);
        Assert.Equal(500f, AreaOfInterestOptions.Default.MarginMeters);
    }

    [Fact]
    public void JoinsTheBoundsOfAllSectionsAndIgnoresSectionsWithout()
    {
        CityWorld world = World(
            [],
            new TestPointsData([new(-100, 0, 0)]),
            new NoBounds(),
            new TestPointsData([new(300, 0, 400)]));

        AreaOfInterest area = AreaOfInterest.Plan(world, new AreaOfInterestOptions { MarginMeters = 10 });

        Assert.Equal(new MapRect(-110, -10, 310, 410), area.Bounds);
        Assert.Equal(new MapRect(-100, 0, 300, 400), area.ContentBounds);
    }

    [Fact]
    public void StaysWithinTheMap()
    {
        CityWorld world = World([], new TestPointsData([new(8600, 0, -8500), new(8000, 0, -8000)]));

        AreaOfInterest area = AreaOfInterest.Plan(world);

        Assert.Equal(new MapRect(7500, -8640, 8640, -7500), area.Bounds);
        Assert.Equal(AreaOfInterestSource.Content, area.Source);
    }

    [Fact]
    public void UsesTheChosenTilesOverEverythingElse()
    {
        CityWorld world = World([new(4, 4)], new TestPointsData([new(0, 0, 0)]));

        AreaOfInterest area = AreaOfInterest.Plan(world, new AreaOfInterestOptions { Tiles = [new(3, 4), new(5, 4)] });

        Assert.Equal(new MapRect(-2880, -960, 2880, 960), area.Bounds);
        Assert.Equal(AreaOfInterestSource.Tiles, area.Source);
        Assert.Equal(new MapRect(0, 0, 0, 0), area.ContentBounds);
    }

    [Fact]
    public void FallsBackToTheUnlockedTiles()
    {
        CityWorld world = World([new(4, 3), new(3, 4), new(4, 4)], new NoBounds());

        AreaOfInterest area = AreaOfInterest.Plan(world, new AreaOfInterestOptions { Tiles = [] });

        Assert.Equal(new MapRect(-2880, -2880, 960, 960), area.Bounds);
        Assert.Equal(AreaOfInterestSource.UnlockedTiles, area.Source);
        Assert.Null(area.ContentBounds);
    }

    [Fact]
    public void FallsBackToTheWholeMap()
    {
        AreaOfInterest area = AreaOfInterest.Plan(World([]));

        Assert.Equal(MapGeometry.Bounds, area.Bounds);
        Assert.Equal(AreaOfInterestSource.Map, area.Source);
    }

    [Fact]
    public void IgnoresContentEntirelyOutsideTheMap()
    {
        CityWorld world = World([new(4, 4)], new TestPointsData([new(20000, 0, 0)]));

        AreaOfInterest area = AreaOfInterest.Plan(world);

        Assert.Equal(AreaOfInterestSource.UnlockedTiles, area.Source);
        Assert.Equal(new MapRect(20000, 0, 20000, 0), area.ContentBounds);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RejectsAnUnusableMargin(float margin) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AreaOfInterest.Plan(World([]), new AreaOfInterestOptions { MarginMeters = margin }));

    private static CityWorld World(TileCoordinate[] unlockedTiles, params ISectionData[] sections)
    {
        DumpManifest manifest = SyntheticDump.CreateManifest();
        foreach (TileCoordinate tile in unlockedTiles)
        {
            manifest.City.UnlockedTiles.Add(tile);
        }

        return new CityWorld("/dumps/test", manifest, sections);
    }

    private sealed class NoBounds : ISectionData
    {
        public IReadOnlyList<SectionStatistic> Statistics => [];

        public MapRect? AreaOfInterestBounds => null;
    }
}
