using System;
using CsToBeamNG.Converter.Imaging;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Imaging;

public sealed class RgbImageTests
{
    [Fact]
    public void StartsFilledAndStoresRowsFromTheTop()
    {
        var image = new RgbImage(3, 2, new Rgb(1, 2, 3));

        image[2, 1] = new Rgb(250, 251, 252);

        Assert.Equal(new Rgb(1, 2, 3), image[0, 0]);
        Assert.Equal(new Rgb(250, 251, 252), image[2, 1]);
        Assert.Equal([1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 250, 251, 252], image.Pixels.ToArray());
    }

    [Fact]
    public void KnowsItsPixels()
    {
        var image = new RgbImage(3, 2, default);

        Assert.True(image.Contains(0, 0));
        Assert.True(image.Contains(2, 1));
        Assert.False(image.Contains(3, 0));
        Assert.False(image.Contains(0, 2));
        Assert.False(image.Contains(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => image[3, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[0, -1] = default);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 5)]
    [InlineData(50000, 50000)]
    public void RejectsUnusableSizes(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RgbImage(width, height, default));

    [Fact]
    public void BlendsWithRounding()
    {
        var under = new Rgb(0, 100, 255);
        var over = new Rgb(255, 0, 255);

        Assert.Equal(under, under.Blend(over, 0));
        Assert.Equal(over, under.Blend(over, 255));
        Assert.Equal(new Rgb(128, 50, 255), under.Blend(over, 128));
    }
}
