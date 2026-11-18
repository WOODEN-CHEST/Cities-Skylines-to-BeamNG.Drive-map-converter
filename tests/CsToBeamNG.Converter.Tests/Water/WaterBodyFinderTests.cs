using System;
using System.Collections.Generic;
using System.Linq;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Water;

public sealed class WaterBodyFinderTests
{
    private const double Tolerance = 1e-9;
    private const double DrawnOffset = CityWater.DrawnOffsetUnits * CityWater.MetersPerUnit;

    // The whole test map: 40 × 40 cells, 16 m apart, from (−320, −320).
    private static readonly MapRect _map = new(-320, -320, 304, 304);

    [Fact]
    public void FindsTheSeaAlongTheMapsEdgeAndAnIsolatedLake()
    {
        var water = new TestWater().WithSea(columns: 3).WithLake(20, 27, 20, 27, surface: 55);

        WaterBodies bodies = water.Find(_map);

        Assert.Equal(40 - DrawnOffset, bodies.SeaSurface!.Value, Tolerance);
        Assert.Equal(40, bodies.SeaLevel, Tolerance);
        Assert.Equal(3 * 40, bodies.SeaCells);
        Lake lake = Assert.Single(bodies.Lakes);
        Assert.Equal(55 - DrawnOffset, lake.Surface, Tolerance);
        Assert.Equal(64, lake.CellCount);
        Assert.Equal(64 * 256, lake.AreaSquareMeters, Tolerance);
        Assert.Equal(0, bodies.SkippedBodies);
        Assert.Equal(0, bodies.DryCellsBelowSea);

        // Land all around stands above the lake, so one box covers the lake and the shore cells around it.
        Assert.Equal(new MapRect(TestWater.CellMin(19), TestWater.CellMin(19), TestWater.CellMax(28), TestWater.CellMax(28)), Assert.Single(lake.Rectangles));
        Assert.True(lake.Bottom < 50 - 4);
    }

    [Fact]
    public void FindsNoSeaWithoutWaterAtTheEdges()
    {
        WaterBodies bodies = new TestWater().WithLake(20, 27, 20, 27, surface: 55).Find(_map);

        Assert.Null(bodies.SeaSurface);
        Assert.Equal(0, bodies.SeaCells);
        Assert.Single(bodies.Lakes);
    }

    [Fact]
    public void LeavesASlopingRiverOut()
    {
        WaterBodies bodies = new TestWater().WithSea(columns: 3).WithRiver(row: 10, firstColumn: 10, lastColumn: 30, startSurface: 70).Find(_map);

        Assert.Empty(bodies.Lakes);
        Assert.Equal(1, bodies.SkippedBodies);
        Assert.Equal(42, bodies.SkippedCells);
        Assert.Equal(70 - 20 - DrawnOffset, bodies.SkippedLowest, Tolerance);
        Assert.Equal(70 - DrawnOffset, bodies.SkippedHighest, Tolerance);
    }

    [Fact]
    public void FindsALargeLakeThatARiverFlowsFrom()
    {
        var water = new TestWater().WithLake(20, 27, 20, 27, surface: 55).WithRiver(row: 24, firstColumn: 28, lastColumn: 37, startSurface: 54);

        WaterBodies bodies = water.Find(_map, new WaterBodyOptions { MinAttachedLakeCells = 40 });

        // The lake takes the river's first cells, which lie within its band; the rest of the river is left out.
        Lake lake = Assert.Single(bodies.Lakes);
        Assert.Equal(66, lake.CellCount);
        Assert.Equal(55 - DrawnOffset, lake.Surface, Tolerance);
        Assert.Equal(1, bodies.SkippedBodies);
        Assert.Equal(18, bodies.SkippedCells);

        // The boxes never cover the river beyond the lake.
        Assert.All(lake.Rectangles, r => Assert.False(r.MaxX > TestWater.CellMax(28) && r.MinZ <= TestWater.Centre(24) && r.MaxZ >= TestWater.Centre(24)));
    }

    [Fact]
    public void LeavesAPoolAlongARiverOutWithTheRiver()
    {
        var water = new TestWater().WithLake(20, 27, 20, 27, surface: 55).WithRiver(row: 24, firstColumn: 28, lastColumn: 37, startSurface: 54);

        WaterBodies bodies = water.Find(_map);

        Assert.Empty(bodies.Lakes);
        Assert.Equal(1, bodies.SkippedBodies);
        Assert.Equal(84, bodies.SkippedCells);
    }

    [Fact]
    public void CountsSmallWaterApart()
    {
        WaterBodies bodies = new TestWater().WithLake(5, 7, 5, 7, surface: 55).Find(_map);

        Assert.Empty(bodies.Lakes);
        Assert.Equal(0, bodies.SkippedBodies);
        Assert.Equal(9, bodies.SmallWaterCells);
    }

    [Fact]
    public void KeepsLakeBoxesOffLowDryGround()
    {
        // A dry hollow beside the lake, lower than the lake's surface: its water mustn't spill into it.
        var water = new TestWater().WithLake(20, 27, 20, 27, surface: 55).WithDryGround(28, 22, height: 50);

        Lake lake = Assert.Single(water.Find(_map).Lakes);

        Assert.True(lake.Rectangles.Count > 1);
        Assert.All(lake.Rectangles, r => Assert.False(Contains(r, TestWater.Centre(28), TestWater.Centre(22))));
        for (int j = 20; j <= 27; j++)
        {
            for (int i = 20; i <= 27; i++)
            {
                Assert.Contains(lake.Rectangles, r => Contains(r, TestWater.Centre(i), TestWater.Centre(j)));
            }
        }

        AssertDisjoint(lake.Rectangles);
    }

    [Fact]
    public void CountsDryGroundBelowTheSea()
    {
        WaterBodies bodies = new TestWater().WithSea(columns: 3).WithDryGround(15, 15, height: 35).WithDryGround(16, 15, height: 35).Find(_map);

        Assert.Equal(2, bodies.DryCellsBelowSea);
    }

    [Fact]
    public void ClipsLakesToTheArea()
    {
        var water = new TestWater().WithLake(20, 27, 20, 27, surface: 55);
        var area = new MapRect(TestWater.Centre(24), -320, 304, 304);

        Lake lake = Assert.Single(water.Find(area).Lakes);

        // The cells whose squares reach into the area: columns 23 to 27.
        Assert.Equal(5 * 8, lake.CellCount);
        Assert.All(lake.Rectangles, r => Assert.True(r.MinX >= area.MinX));
        Assert.Equal(area.MinX, lake.Bounds.MinX);
    }

    private static bool Contains(MapRect rectangle, double x, double z) =>
        x > rectangle.MinX && x < rectangle.MaxX && z > rectangle.MinZ && z < rectangle.MaxZ;

    private static void AssertDisjoint(IReadOnlyList<MapRect> rectangles)
    {
        for (int a = 0; a < rectangles.Count; a++)
        {
            for (int b = a + 1; b < rectangles.Count; b++)
            {
                Assert.True(rectangles[a].Intersect(rectangles[b]) is not { IsEmpty: false }, $"Boxes {a} and {b} overlap.");
            }
        }
    }

    // A small map's water: land at 60 m everywhere unless a method says otherwise; the sea level is 40 m.
    internal sealed class TestWater
    {
        public const int Size = 40;
        public const float Spacing = 16;
        public const float Origin = -320;

        private readonly double[] _ground = Enumerable.Repeat(60.0, Size * Size).ToArray();
        private readonly double[] _depth = new double[Size * Size];

        public static GridGeometry Grid { get; } = new(Origin, Origin, Spacing, Size);

        public static float Centre(int index) => Origin + (index * Spacing);

        public static float CellMin(int index) => Centre(index) - (Spacing / 2);

        public static float CellMax(int index) => Centre(index) + (Spacing / 2);

        /// <summary>Sea along the west edge: ground at 30 m with 10 m of water.</summary>
        public TestWater WithSea(int columns)
        {
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    Set(i, j, 30, 10);
                }
            }

            return this;
        }

        /// <summary>A lake over a block of cells, with its bed 5 m below the surface.</summary>
        public TestWater WithLake(int firstI, int lastI, int firstJ, int lastJ, double surface)
        {
            for (int j = firstJ; j <= lastJ; j++)
            {
                for (int i = firstI; i <= lastI; i++)
                {
                    Set(i, j, surface - 5, 5);
                }
            }

            return this;
        }

        /// <summary>A river two cells wide along a row, its surface falling by 1 m per cell, 2 m deep.</summary>
        public TestWater WithRiver(int row, int firstColumn, int lastColumn, double startSurface)
        {
            for (int i = firstColumn; i <= lastColumn; i++)
            {
                double surface = startSurface - (i - firstColumn);
                Set(i, row, surface - 2, 2);
                Set(i, row + 1, surface - 2, 2);
            }

            return this;
        }

        public TestWater WithDryGround(int i, int j, double height)
        {
            Set(i, j, height, 0);
            return this;
        }

        public WaterBodies Find(MapRect area, WaterBodyOptions? options = null)
        {
            var content = new WaterSectionContent(40, Grid, Units(_ground), Units(_depth), new ushort[Size * Size], new short[Size * Size], new short[Size * Size]);
            var water = new CityWater(content, []);
            return WaterBodyFinder.Find(water, Ground, area, options);
        }

        // The ground at a position: the nearest cell's.
        private double Ground(double x, double z)
        {
            int i = Math.Clamp((int)Math.Round((x - Origin) / Spacing), 0, Size - 1);
            int j = Math.Clamp((int)Math.Round((z - Origin) / Spacing), 0, Size - 1);
            return _ground[(j * Size) + i];
        }

        private void Set(int i, int j, double ground, double depth)
        {
            _ground[(j * Size) + i] = ground;
            _depth[(j * Size) + i] = depth;
        }

        private static ushort[] Units(double[] meters) => [.. meters.Select(m => (ushort)Math.Round(m * 64))];
    }
}
