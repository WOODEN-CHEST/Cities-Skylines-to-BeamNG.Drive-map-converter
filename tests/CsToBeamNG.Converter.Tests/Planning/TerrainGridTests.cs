using System;
using System.Numerics;
using CsToBeamNG.Converter.Coordinates;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Planning;

public sealed class TerrainGridTests
{
    // The game's detail lattice: 4 m apart from the map's corner.
    private static readonly GridGeometry _lattice = new(-8640, -8640, 4, 4321);

    [Theory]
    [InlineData(3000, 8192, 4096, 1)]
    [InlineData(4773, 8192, 8192, 1)]
    [InlineData(4773, 4096, 4096, 2)]
    [InlineData(9000, 8192, 8192, 2)]
    [InlineData(12000, 4096, 4096, 4)]
    [InlineData(500, 8192, 512, 1)]
    [InlineData(100, 8192, 128, 1)]
    public void ChoosesTheFinestSquaresThatFit(float width, int maxSize, int expectedSize, float expectedSquareSize)
    {
        var area = new MapRect(-1000, -2000, -1000 + width, -2000 + (width / 2));

        TerrainGrid grid = TerrainGrid.Plan(area, _lattice, MapGeometry.Bounds, new TerrainGridOptions { MaxSize = maxSize });

        Assert.Equal(expectedSize, grid.Size);
        Assert.Equal(expectedSquareSize, grid.SquareSize);
        Assert.True(grid.CoversArea);
        Assert.Equal((expectedSize - 1) * (double)expectedSquareSize, grid.ExtentMeters);
    }

    [Fact]
    public void CentresTheGridOnTheLattice()
    {
        var area = new MapRect(-2069.71f, -3329.44f, 1435.77f, 1443.71f);

        TerrainGrid grid = TerrainGrid.Plan(area, _lattice, MapGeometry.Bounds);

        Assert.Equal(0, (grid.Coverage.MinX + 8640) % 4);
        Assert.Equal(0, (grid.Coverage.MinZ + 8640) % 4);
        Assert.True(grid.Coverage.MinX <= area.MinX && grid.Coverage.MaxX >= area.MaxX);
        Assert.True(grid.Coverage.MinZ <= area.MinZ && grid.Coverage.MaxZ >= area.MaxZ);

        // Centred within one lattice step.
        double areaCentreX = (area.MinX + area.MaxX) / 2.0;
        double gridCentreX = (grid.Coverage.MinX + grid.Coverage.MaxX) / 2.0;
        Assert.InRange(gridCentreX - areaCentreX, -2.01, 2.01);
    }

    [Fact]
    public void StaysInsideTheMapWhereItFits()
    {
        var area = new MapRect(7000, -100, 8500, 400);

        TerrainGrid grid = TerrainGrid.Plan(area, _lattice, MapGeometry.Bounds);

        Assert.Equal(2048, grid.Size);
        Assert.True(grid.Coverage.MaxX <= 8640);
        Assert.True(grid.Coverage.MinX <= area.MinX);
        Assert.True(grid.CoversArea);
    }

    [Fact]
    public void CoversTheMiddleOfAnAreaThatIsTooLarge()
    {
        var area = new MapRect(-8640, -8640, 8640, 8640);

        TerrainGrid grid = TerrainGrid.Plan(area, _lattice, MapGeometry.Bounds, new TerrainGridOptions { MaxSize = 2048 });

        Assert.False(grid.CoversArea);
        Assert.Equal(2048, grid.Size);
        Assert.Equal(4, grid.SquareSize);
        Assert.InRange(grid.Coverage.MinX + grid.Coverage.MaxX, -8.01, 8.01);
    }

    [Fact]
    public void RelatesSamplesToTheCityThroughTheCoordinateMapping()
    {
        TerrainGrid grid = TerrainGrid.Plan(new MapRect(100, 200, 900, 600), _lattice, MapGeometry.Bounds);
        float s = grid.SquareSize;

        Assert.Equal((grid.Coverage.MinX, grid.Coverage.MinZ), ((float)grid.ToCitiesSkylines(0, 0).X, (float)grid.ToCitiesSkylines(0, 0).Z));
        Assert.Equal(grid.ToCitiesSkylines(0, 0).X + (3 * s), grid.ToCitiesSkylines(3, 0).X);
        Assert.Equal(grid.ToCitiesSkylines(0, 0).Z + (5 * s), grid.ToCitiesSkylines(0, 5).Z);

        // A sample's BeamNG position is the mapped city position: columns along BeamNG's X, rows along its Y.
        (double x, double z) = grid.ToCitiesSkylines(7, 11);
        Assert.Equal(CoordinateMapping.ToBeamNG(new Vector3((float)x, 42, (float)z)), grid.ToBeamNG(7, 11, 42));
        Assert.Equal(new Vector2(grid.Coverage.MinX, grid.Coverage.MinZ), grid.BeamNGPosition);
        Vector3 origin = grid.ToBeamNG(0, 0, 0);
        Assert.Equal(origin + new Vector3(s, 0, 0), grid.ToBeamNG(1, 0, 0));
        Assert.Equal(origin + new Vector3(0, s, 0), grid.ToBeamNG(0, 1, 0));
    }

    [Fact]
    public void FindsSamplesFromCityPositions()
    {
        TerrainGrid grid = TerrainGrid.Plan(new MapRect(100, 200, 900, 600), _lattice, MapGeometry.Bounds);
        (double x, double z) = grid.ToCitiesSkylines(12, 34);

        Assert.Equal((12.0, 34.0), grid.ToSample(x, z));
        Assert.Equal((12.5, 34.0), grid.ToSample(x + (grid.SquareSize / 2.0), z));

        Assert.True(grid.TryGetSampleRange(new MapRect((float)x - 0.5f, (float)z, (float)x + 2.5f, (float)z + 1), out int c0, out int c1, out int r0, out int r1));
        Assert.Equal((12, 14, 34, 35), (c0, c1, r0, r1));
        Assert.False(grid.TryGetSampleRange(new MapRect(-8000, -8000, -7000, -7000), out _, out _, out _, out _));
    }

    [Fact]
    public void ValidatesItsOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainGridOptions { MaxSize = 3000 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainGridOptions { MaxSize = 16384 });
        Assert.Throws<ArgumentException>(() => new TerrainGridOptions { SquareSizes = [2, 1] });
        Assert.Throws<ArgumentException>(() => new TerrainGridOptions { SquareSizes = [] });
        Assert.Throws<ArgumentException>(() => TerrainGrid.Plan(
            new MapRect(0, 0, 10, 10), _lattice, MapGeometry.Bounds, new TerrainGridOptions { MinSize = 1024, MaxSize = 512 }));
    }
}
