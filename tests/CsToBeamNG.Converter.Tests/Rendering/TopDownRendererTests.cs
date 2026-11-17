using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.Rendering.Layers;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class TopDownRendererTests : IDisposable
{
    // The whole map at 90 pixels: 192 m per pixel, so tile (x, z) covers columns 10x to 10x + 9 and, north up,
    // rows 10 (8 − z) to 10 (8 − z) + 9. The origin is at pixel (45, 45).
    private const int MapPixels = 90;

    private static readonly Rgb _unlocked = GroundLayer.Color.Blend(TileGridLayer.UnlockedTint, TileGridLayer.UnlockedAlpha);

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void DrawsTheMapWithNorthUpAndEastRight()
    {
        // Unlocked: the tile east of the centre and the one north of it.
        RgbImage image = RenderMap(World(new TileCoordinate(5, 4), new TileCoordinate(4, 5)));

        Assert.Equal(_unlocked, image[55, 45]);
        Assert.Equal(_unlocked, image[45, 35]);
        Assert.Equal(GroundLayer.Color, image[35, 45]);
        Assert.Equal(GroundLayer.Color, image[45, 55]);
    }

    [Fact]
    public void DrawsTheTileGrid()
    {
        // Without tiles or content, the area of interest is the whole map, outlined along the image's edge.
        RgbImage image = RenderMap(World());

        Assert.Equal(AreaOfInterestLayer.Color, image[0, 0]);
        Assert.Equal(AreaOfInterestLayer.Color, image[89, 89]);
        Assert.Equal(TileGridLayer.GridColor, image[9, 5]);
        Assert.Equal(TileGridLayer.GridColor, image[10, 5]);
        Assert.Equal(TileGridLayer.GridColor, image[5, 19]);
        Assert.Equal(TileGridLayer.GridColor, image[5, 20]);
        Assert.Equal(GroundLayer.Color, image[5, 5]);
    }

    [Fact]
    public void DrawsTheAxesFromTheOrigin()
    {
        RgbImage image = RenderMap(World());

        Assert.Equal(AxesLayer.XColor, image[50, 45]);
        Assert.Equal(AxesLayer.XColor, image[50, 46]);
        Assert.Equal(AxesLayer.ZColor, image[45, 40]);
        Assert.Equal(AxesLayer.ZColor, image[46, 40]);
        Assert.Equal(GroundLayer.Color, image[42, 52]);
    }

    [Fact]
    public void OutlinesTheAreaOfInterestOverTheTiles()
    {
        // The area of interest is the unlocked tiles: x -960 to 2880, z -960 to 2880.
        RgbImage image = RenderMap(World(new TileCoordinate(5, 4), new TileCoordinate(4, 5)));

        Assert.Equal(AreaOfInterestLayer.Color, image[40, 30]);
        Assert.Equal(AreaOfInterestLayer.Color, image[41, 31]);
        Assert.Equal(AreaOfInterestLayer.Color, image[59, 49]);
        Assert.Equal(TileGridLayer.UnlockedEdgeColor, image[49, 35]);
        Assert.Equal(_unlocked, image[42, 33]);
    }

    [Fact]
    public void LeavesTheBackgroundOutsideTheMap()
    {
        CityWorld world = World();
        var view = new RenderView(new MapRect(-9000, -1000, -8000, 0), 100);

        RgbImage image = TopDownRenderer.Render(world, AreaOfInterest.Plan(world), view);

        Assert.Equal(TopDownRenderer.Background, image[10, 50]);
        Assert.Equal(GroundLayer.Color, image[60, 50]);
    }

    [Fact]
    public void GivesTheSamePixelsEveryTime()
    {
        CityWorld world = World(new TileCoordinate(3, 3));

        Assert.Equal(RenderMap(world).Pixels.ToArray(), RenderMap(world).Pixels.ToArray());
    }

    [Fact]
    public void DrawsTheLayersOfLoadedSections()
    {
        string folder = new SyntheticDump().WithPoints(new Vector3(-4000, 10, 2000), new Vector3(4000, 10, 2000)).WriteTo(_temp.PathOf("dump"));
        CityWorld world = WorldLoader.Load(folder, new WorldLoadOptions { Loaders = new SectionLoaderRegistry().Add(new TestPointsLoader()) }).World;
        AreaOfInterest area = AreaOfInterest.Plan(world);
        IReadOnlyList<IRenderLayer> defaults = RenderLayers.CreateDefault();
        int aboveTiles = defaults.Select(layer => layer.Name).ToList().IndexOf("tiles") + 1;
        IRenderLayer[] layers = [.. defaults.Take(aboveTiles), new TestPointsLayer(), .. defaults.Skip(aboveTiles)];

        RgbImage image = TopDownRenderer.Render(world, area, new RenderView(MapGeometry.Bounds, MapPixels), layers);

        // The line z = 2000 runs along row 34, from x = -4000 (column 24) to 4000 (column 65).
        Assert.Equal(TestPointsLayer.Color, image[24, 34]);
        Assert.Equal(TestPointsLayer.Color, image[40, 34]);
        Assert.Equal(TestPointsLayer.Color, image[65, 34]);
        Assert.NotEqual(TestPointsLayer.Color, image[66, 34]);
        Assert.Equal(new MapRect(-4500, 1500, 4500, 2500), area.Bounds);
    }

    [Fact]
    public void HasTheDefaultLayersInDrawingOrder()
    {
        IReadOnlyList<IRenderLayer> layers = RenderLayers.CreateDefault();

        Assert.Equal(["ground", "terrain", "water", "tiles", "networks", "trees", "buildings", "props", "area", "axes"], layers.Select(layer => layer.Name));
        Assert.All(layers, layer => Assert.False(string.IsNullOrWhiteSpace(layer.Description)));
    }

    [Fact]
    public void LayersNeedAContext()
    {
        foreach (IRenderLayer layer in RenderLayers.CreateDefault())
        {
            Assert.Throws<ArgumentNullException>(() => layer.Draw(null!));
        }
    }

    [Theory]
    [InlineData(90, 90, 8)]
    [InlineData(1024, 683, 42)]
    [InlineData(4096, 4096, 256)]
    public void ScalesTheArrowsWithTheImage(int width, int height, int length) =>
        Assert.Equal(length, AxesLayer.ArrowLength(width, height));

    [Fact]
    public void SkipsTheAxesWhenTheOriginIsFarOutside()
    {
        CityWorld world = World();
        var view = new RenderView(new MapRect(5000, 5000, 6000, 6000), 100);

        RgbImage image = TopDownRenderer.Render(world, AreaOfInterest.Plan(world), view, [new AxesLayer()]);

        Assert.All(Enumerable.Range(0, 100), y => Assert.Equal(TopDownRenderer.Background, image[0, y]));
        Assert.DoesNotContain(AxesLayer.XColor, Colors(image));
        Assert.DoesNotContain(AxesLayer.ZColor, Colors(image));
    }

    private static RgbImage RenderMap(CityWorld world) =>
        TopDownRenderer.Render(world, AreaOfInterest.Plan(world), new RenderView(MapGeometry.Bounds, MapPixels));

    private static CityWorld World(params TileCoordinate[] unlockedTiles)
    {
        DumpManifest manifest = SyntheticDump.CreateManifest();
        foreach (TileCoordinate tile in unlockedTiles)
        {
            manifest.City.UnlockedTiles.Add(tile);
        }

        return new CityWorld("/dumps/test", manifest, []);
    }

    private static HashSet<Rgb> Colors(RgbImage image)
    {
        var colors = new HashSet<Rgb>();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                colors.Add(image[x, y]);
            }
        }

        return colors;
    }
}
