using System;
using CsToBeamNG.Converter.Terrain;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Terrain;

public sealed class MonotoneCubicTests
{
    [Fact]
    public void PassesThroughTheValues()
    {
        Assert.Equal(3, MonotoneCubic.Interpolate(1, 3, 7, 4, 0));
        Assert.Equal(7, MonotoneCubic.Interpolate(1, 3, 7, 4, 1));
    }

    [Fact]
    public void ReproducesStraightLines()
    {
        foreach (double t in new[] { 0.1, 0.25, 0.5, 0.9 })
        {
            Assert.Equal(12 + (2 * t), MonotoneCubic.Interpolate(10, 12, 14, 16, t), 12);
        }
    }

    [Fact]
    public void NeverLeavesTheRangeOfTheTwoMiddleValues()
    {
        // Deterministic data with steps, peaks and plateaus, from a simple generator.
        uint state = 12345;
        double Next()
        {
            state = (state * 1664525) + 1013904223;
            return (state >> 8) % 1000 / 10.0;
        }

        for (int n = 0; n < 2000; n++)
        {
            double p0 = Next();
            double p1 = Next();
            double p2 = Next();
            double p3 = Next();
            for (int step = 0; step <= 20; step++)
            {
                double value = MonotoneCubic.Interpolate(p0, p1, p2, p3, step / 20.0);
                Assert.InRange(value, Math.Min(p1, p2) - 1e-9, Math.Max(p1, p2) + 1e-9);
            }
        }
    }

    [Fact]
    public void IsFlatAtATurnAndOnALevelStretch()
    {
        // At a peak the slope is 0, so the curve leaves it only to second order.
        Assert.True(MonotoneCubic.Interpolate(0, 10, 0, 0, 0.01) > 9.99);
        Assert.Equal(5, MonotoneCubic.Interpolate(1, 5, 5, 9, 0.5));
    }

    [Fact]
    public void IsSmootherThanLinearInterpolation()
    {
        // Along a bend (gentle, then steep) the cubic follows the curvature: below the chord, as the bend is convex.
        double cubic = MonotoneCubic.Interpolate(0, 1, 4, 9, 0.5);
        Assert.True(cubic < 2.5);
        Assert.True(cubic > 1);
    }
}
