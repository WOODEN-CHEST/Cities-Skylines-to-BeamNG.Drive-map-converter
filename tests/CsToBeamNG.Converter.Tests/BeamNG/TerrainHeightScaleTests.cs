using System;
using CsToBeamNG.Converter.BeamNG.Terrain;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class TerrainHeightScaleTests
{
    [Fact]
    public void DividesByTwoToTheSixteenth()
    {
        var scale = new TerrainHeightScale(100);

        // Heights the game reported for the hello level (maxHeight 100) with get_ground_at_point.
        Assert.Equal(19.99969, scale.ToMeters(13107), 5);
        Assert.Equal(60.00061, scale.ToMeters(39322), 5);
        Assert.Equal(32768, scale.ToStored(50));
        Assert.Equal(100.0 * 65535 / 65536, scale.HighestHeight, 10);
    }

    [Fact]
    public void RoundsToTheNearestStep()
    {
        var scale = new TerrainHeightScale(1000);
        var random = new Random(42);
        for (int i = 0; i < 10_000; i++)
        {
            double height = random.NextDouble() * scale.HighestHeight;
            Assert.True(Math.Abs(scale.ToMeters(scale.ToStored(height)) - height) <= (scale.Resolution / 2) + 1e-9);
        }
    }

    [Fact]
    public void RejectsHeightsOutsideTheRange()
    {
        var scale = new TerrainHeightScale(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => scale.ToStored(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.ToStored(100));
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.ToStored(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainHeightScale(0));
        Assert.Equal(ushort.MaxValue, scale.ToStored(scale.HighestHeight));
    }
}
