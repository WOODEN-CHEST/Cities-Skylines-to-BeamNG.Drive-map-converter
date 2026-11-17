using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class MapAreaSyntaxTests
{
    [Fact]
    public void ReadsOneTile()
    {
        Assert.True(MapAreaSyntax.TryParseTiles("4,4", out IReadOnlyList<TileCoordinate>? tiles, out string? error));

        Assert.Equal([new TileCoordinate(4, 4)], tiles);
        Assert.Null(error);
    }

    [Fact]
    public void ReadsABlockOfTilesRowByRow()
    {
        Assert.True(MapAreaSyntax.TryParseTiles(" 3-5 , 3-4 ", out IReadOnlyList<TileCoordinate>? tiles, out _));

        Assert.Equal([new(3, 3), new(4, 3), new(5, 3), new(3, 4), new(4, 4), new(5, 4)], tiles);
        Assert.True(MapAreaSyntax.TryParseTiles("0-8,8", out tiles, out _));
        Assert.Equal(9, tiles.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("4")]
    [InlineData("4,4,4")]
    [InlineData("9,4")]
    [InlineData("4,-1")]
    [InlineData("5-3,4")]
    [InlineData("3-4-5,4")]
    [InlineData("a,b")]
    [InlineData("+4,4")]
    [InlineData("4.0,4")]
    public void RejectsWhatIsntABlockOfTiles(string text)
    {
        Assert.False(MapAreaSyntax.TryParseTiles(text, out IReadOnlyList<TileCoordinate>? tiles, out string? error));

        Assert.Null(tiles);
        Assert.StartsWith($"'{text}' is not a block of tiles. Use x,z with each a tile number from 0 to 8", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aoi")]
    [InlineData(" AOI ")]
    public void ReadsTheAreaOfInterest(string text)
    {
        Assert.True(MapAreaSyntax.TryParseArea(text, out RenderArea? area, out _));

        Assert.Same(RenderArea.AreaOfInterest, area);
    }

    [Fact]
    public void ReadsTheWholeMapTilesAndRectangles()
    {
        Assert.True(MapAreaSyntax.TryParseArea("map", out RenderArea? map, out _));
        Assert.Equal(MapGeometry.Bounds, map.Bounds);

        Assert.True(MapAreaSyntax.TryParseArea("3-4,4", out RenderArea? tiles, out _));
        Assert.Equal(new MapRect(-2880, -960, 960, 960), tiles.Bounds);
        Assert.Equal("tiles 3-4,4", tiles.Description);
        Assert.Equal("3-4,4", tiles.ToString());

        Assert.True(MapAreaSyntax.TryParseArea("-500,-300.5,1500,7e2", out RenderArea? rectangle, out _));
        Assert.Equal(new MapRect(-500, -300.5f, 1500, 700), rectangle.Bounds);
        Assert.Equal("x -500 to 1500, z -300.5 to 700", rectangle.Description);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("1,2,3")]
    [InlineData("0,0,0,10")]
    [InlineData("0,10,10,0")]
    [InlineData("NaN,0,1,1")]
    [InlineData("0,0,1e40,1")]
    [InlineData("0,0,1,1,1")]
    [InlineData("9,9")]
    public void RejectsWhatIsntAnArea(string text)
    {
        Assert.False(MapAreaSyntax.TryParseArea(text, out RenderArea? area, out string? error));

        Assert.Null(area);
        Assert.StartsWith($"'{text}' is not an area. Use aoi (the area of interest), map (the whole map)", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsNumbersWithTheInvariantCulture()
    {
        using var scope = new CultureScope("lv-LV");

        Assert.True(MapAreaSyntax.TryParseArea("-0.5,0,10.25,10", out RenderArea? area, out _));
        Assert.Equal(new MapRect(-0.5f, 0, 10.25f, 10), area.Bounds);
        Assert.False(MapAreaSyntax.TryParseArea("-0,5,0,10,25,10", out _, out _));
    }
}
