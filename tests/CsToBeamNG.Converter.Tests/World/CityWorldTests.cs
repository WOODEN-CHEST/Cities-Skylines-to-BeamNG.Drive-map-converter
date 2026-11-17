using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.World;

public sealed class CityWorldTests
{
    [Fact]
    public void HoldsTheManifestAndTheLoadedSections()
    {
        DumpManifest manifest = SyntheticDump.CreateManifest();
        manifest.City.UnlockedTiles.Add(new TileCoordinate(4, 4));
        var first = new TestPointsData([new(1, 2, 3)]);
        var second = new TestPointsData([]);
        var other = new OtherData();

        var world = new CityWorld("/dumps/city", manifest, [first, other, second]);

        Assert.Equal("/dumps/city", world.DumpFolder);
        Assert.Same(manifest, world.Manifest);
        Assert.Equal([new TileCoordinate(4, 4)], world.UnlockedTiles);
        Assert.Equal([first, other, second], world.Sections);
        Assert.Same(first, world.Find<TestPointsData>());
        Assert.Same(other, world.Find<OtherData>());
    }

    [Fact]
    public void FindsNothingThatWasntLoaded() =>
        Assert.Null(new CityWorld("/dumps/city", SyntheticDump.CreateManifest(), []).Find<TestPointsData>());

    [Fact]
    public void RejectsMissingSectionData() =>
        Assert.Throws<ArgumentException>(() => new CityWorld("/dumps/city", SyntheticDump.CreateManifest(), [null!]));

    private sealed class OtherData : ISectionData
    {
        public IReadOnlyList<SectionStatistic> Statistics => [];

        public MapRect? AreaOfInterestBounds => null;
    }
}
