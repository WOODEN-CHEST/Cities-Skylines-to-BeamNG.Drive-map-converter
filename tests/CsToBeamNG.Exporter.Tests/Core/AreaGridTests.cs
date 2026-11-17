using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class AreaGridTests
{
    [Fact]
    public void PlacesTheVanillaGridInTheCentreOfTheMap()
    {
        // The vanilla grid covers the central 5 x 5 tiles; its middle entry is the map's centre tile.
        int[] grid = new int[25];
        grid[12] = 1;

        IList<TileCoordinate> tiles = AreaGrid.ToTiles(grid, out string? problem);

        Assert.Null(problem);
        Assert.Equal([new TileCoordinate(4, 4)], tiles);
    }

    [Fact]
    public void ReadsTheVanillaGridRowByRow()
    {
        // Index z * 5 + x; the values are the order in which the tiles were unlocked.
        int[] grid = new int[25];
        grid[(1 * 5) + 4] = 3;
        grid[(0 * 5) + 0] = 2;
        grid[(4 * 5) + 1] = 1;

        IList<TileCoordinate> tiles = AreaGrid.ToTiles(grid, out string? problem);

        Assert.Null(problem);
        Assert.Equal([new TileCoordinate(2, 2), new TileCoordinate(6, 3), new TileCoordinate(3, 6)], tiles);
    }

    [Fact]
    public void ReadsTheWholeMapGridOf81TilesDirectly()
    {
        int[] grid = new int[81];
        grid[0] = 1;
        grid[(3 * 9) + 7] = 2;
        grid[80] = 3;

        IList<TileCoordinate> tiles = AreaGrid.ToTiles(grid, out string? problem);

        Assert.Null(problem);
        Assert.Equal([new TileCoordinate(0, 0), new TileCoordinate(7, 3), new TileCoordinate(8, 8)], tiles);
    }

    [Fact]
    public void ReportsAMissingGrid()
    {
        IList<TileCoordinate> tiles = AreaGrid.ToTiles(null, out string? problem);

        Assert.Empty(tiles);
        Assert.Contains("m_areaGrid is null", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(36)]
    [InlineData(121)]
    public void ReportsGridsThatCannotBeCentredOnTheMap(int length)
    {
        int[] grid = new int[length];
        Array.Fill(grid, 1);

        IList<TileCoordinate> tiles = AreaGrid.ToTiles(grid, out string? problem);

        Assert.Empty(tiles);
        Assert.NotNull(problem);
        Assert.Contains(length.ToString(CultureInfo.InvariantCulture) + " entries", problem, StringComparison.Ordinal);
    }
}
