using System;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class RenderViewTests
{
    [Theory]
    [InlineData(1000, 1000, 100, 100, 100, 10)]
    [InlineData(2000, 500, 400, 400, 100, 5)]
    [InlineData(300, 1200, 120, 30, 120, 10)]
    [InlineData(1000, 333, 100, 100, 33, 10)]
    [InlineData(1000, 1, 100, 100, 1, 10)]
    public void GivesTheLongerSideTheRequestedPixels(float width, float depth, int size, int expectedWidth, int expectedHeight, double metersPerPixel)
    {
        var view = new RenderView(new MapRect(-100, 50, -100 + width, 50 + depth), size);

        Assert.Equal(expectedWidth, view.Width);
        Assert.Equal(expectedHeight, view.Height);
        Assert.Equal(metersPerPixel, view.MetersPerPixel, 12);
    }

    [Fact]
    public void PutsNorthUpAndEastRight()
    {
        var view = new RenderView(new MapRect(-500, -200, 500, 800), 100);

        Assert.Equal(0, view.ToPixelX(-500));
        Assert.Equal(100, view.ToPixelX(500));
        Assert.Equal(0, view.ToPixelY(800));
        Assert.Equal(100, view.ToPixelY(-200));
        Assert.Equal(-495, view.ToWorldX(0.5));
        Assert.Equal(795, view.ToWorldZ(0.5));
        Assert.Equal(123.25, view.ToWorldX(view.ToPixelX(123.25)), 9);
        Assert.Equal(-77.5, view.ToWorldZ(view.ToPixelY(-77.5)), 9);
    }

    [Fact]
    public void RejectsAnAreaWithoutSize() =>
        Assert.Throws<ArgumentException>(() => new RenderView(new MapRect(0, 0, 0, 10), 100));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(RenderView.MaxSizePixels + 1)]
    public void RejectsUnusableSizes(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderView(new MapRect(0, 0, 10, 10), size));
}
