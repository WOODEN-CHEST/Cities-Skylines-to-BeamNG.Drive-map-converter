using System;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.World;

public sealed class MapRectTests
{
    [Fact]
    public void MeasuresAlongXAndZ()
    {
        var rect = new MapRect(-100, 20, 300, 70);

        Assert.Equal(400f, rect.Width);
        Assert.Equal(50f, rect.Depth);
        Assert.False(rect.IsEmpty);
        Assert.True(new MapRect(5, 5, 5, 10).IsEmpty);
        Assert.True(new MapRect(5, 5, 10, 5).IsEmpty);
    }

    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(0, 1, 1, 0)]
    [InlineData(float.NaN, 0, 1, 1)]
    [InlineData(0, 0, float.PositiveInfinity, 1)]
    [InlineData(0, float.NegativeInfinity, 1, 1)]
    [InlineData(0, 0, 1, float.NaN)]
    public void RejectsInvalidRanges(float minX, float minZ, float maxX, float maxZ) =>
        Assert.Throws<ArgumentException>(() => new MapRect(minX, minZ, maxX, maxZ));

    [Fact]
    public void CombinesRectangles()
    {
        var a = new MapRect(0, 0, 10, 10);
        var b = new MapRect(5, -5, 20, 5);

        Assert.Equal(new MapRect(0, -5, 20, 10), a.Union(b));
        Assert.Equal(new MapRect(5, 0, 10, 5), a.Intersect(b));
        Assert.Equal(new MapRect(10, 0, 10, 10), a.Intersect(new MapRect(10, 0, 30, 10)));
        Assert.Null(a.Intersect(new MapRect(11, 0, 30, 10)));
        Assert.Null(a.Intersect(new MapRect(0, -10, 10, -1)));
    }

    [Fact]
    public void GrowsByAMargin()
    {
        var rect = new MapRect(0, 0, 10, 10);

        Assert.Equal(new MapRect(-2.5f, -2.5f, 12.5f, 12.5f), rect.Expand(2.5f));
        Assert.Equal(rect, rect.Expand(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.Expand(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.Expand(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.Expand(float.PositiveInfinity));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    [InlineData("sv-SE")]
    public void FormatsWithTheInvariantCulture(string culture)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal("x -3840 to 1920.5, z -1920.25 to 0.13", new MapRect(-3840, -1920.25f, 1920.5f, 0.125f).ToString());
    }
}
