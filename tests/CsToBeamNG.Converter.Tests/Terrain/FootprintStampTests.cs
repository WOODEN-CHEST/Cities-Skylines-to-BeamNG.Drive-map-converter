using System;
using System.Numerics;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class FootprintStampTests
{
    // A strip 20 m long and 8 m wide that rises from 10 m at x = 0 to 12 m at x = 20, like a road bed.
    private static readonly Vector3[] _strip =
    [
        new(0, 10, -4), new(20, 12, -4), new(20, 12, 4),
        new(0, 10, -4), new(20, 12, 4), new(0, 10, 4),
    ];

    [Fact]
    public void SetsTheFootprintsHeightInside()
    {
        var stamp = new FootprintStamp(_strip, StampMode.Set, falloffMeters: 0);

        Assert.Equal(11, stamp.Apply(10, 1, 50), 9);
        Assert.Equal(10.5, stamp.Apply(5, -3.5, 0), 9);
        Assert.Equal(12, stamp.Apply(20, 4, 3), 9);
    }

    [Fact]
    public void LowersOrRaisesOnlyWhereNeeded()
    {
        var lower = new FootprintStamp(_strip, StampMode.Lower, falloffMeters: 0);
        var raise = new FootprintStamp(_strip, StampMode.Raise, falloffMeters: 0);

        Assert.Equal(11, lower.Apply(10, 0, 30), 9);
        Assert.Equal(5, lower.Apply(10, 0, 5), 9);
        Assert.Equal(30, raise.Apply(10, 0, 30), 9);
        Assert.Equal(11, raise.Apply(10, 0, 5), 9);
    }

    [Fact]
    public void BlendsBackToTheTerrainWithinTheFalloff()
    {
        var stamp = new FootprintStamp(_strip, StampMode.Set, falloffMeters: 4);

        // 2 m beside the edge, halfway through the falloff: a smoothstep weight of 0.5 between 11 m and the terrain.
        Assert.Equal(11 + ((31 - 11) * 0.5), stamp.Apply(10, 6, 31), 9);

        // At the edge the footprint's height; at the falloff's end and beyond, the terrain's own.
        Assert.Equal(11, stamp.Apply(10, 4, 31), 9);
        Assert.Equal(31, stamp.Apply(10, 8, 31), 9);
        Assert.Equal(31, stamp.Apply(10, 20, 31), 9);

        // Beyond the strip's end, the height of its nearest corner.
        Assert.Equal(12 + ((40 - 12) * 0.5), stamp.Apply(22, 0, 40), 9);
    }

    [Fact]
    public void BoundsIncludeTheFalloff()
    {
        Assert.Equal(new MapRect(-4, -8, 24, 8), new FootprintStamp(_strip, StampMode.Set, falloffMeters: 4).Bounds);
        Assert.Equal(new MapRect(0, -4, 20, 4), new FootprintStamp(_strip, StampMode.Set, falloffMeters: 0).Bounds);
    }

    [Fact]
    public void RejectsInvalidFootprints()
    {
        Assert.Throws<ArgumentException>(() => new FootprintStamp([], StampMode.Set, 0));
        Assert.Throws<ArgumentException>(() => new FootprintStamp([Vector3.Zero, Vector3.One], StampMode.Set, 0));
        Assert.Throws<ArgumentException>(() => new FootprintStamp([new(float.NaN, 0, 0), Vector3.One, Vector3.UnitX], StampMode.Set, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootprintStamp(_strip, StampMode.Set, -1));
    }
}
