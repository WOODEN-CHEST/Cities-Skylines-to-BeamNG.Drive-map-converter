using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.Rendering.Layers;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class TerrainWaterLayerTests
{
    // The miniature map's 144 m square at 36 pixels: 4 m per pixel, x = −72 at column 0, z = 72 at row 0.
    private static readonly RenderView _view = new(new MapRect(-TestTerrain.HalfSize, -TestTerrain.HalfSize, TestTerrain.HalfSize, TestTerrain.HalfSize), 36);

    [Fact]
    public void LightsSlopesThatFaceTheNorthWest()
    {
        // The same height at the centre: one terrain rises towards the south-east, so it faces the light; the other
        // rises towards the north-west and faces away from it.
        RgbImage facingLight = Render(new CityTerrain(TestTerrain.Create((x, z) => 50, (x, z) => 50 + ((x - z) / 4))), new HillshadeLayer());
        RgbImage facingAway = Render(new CityTerrain(TestTerrain.Create((x, z) => 50, (x, z) => 50 - ((x - z) / 4))), new HillshadeLayer());
        RgbImage flat = Render(new CityTerrain(TestTerrain.Create((x, z) => 50, (x, z) => 50)), new HillshadeLayer());

        Assert.True(Brightness(facingLight[18, 18]) > Brightness(facingAway[18, 18]) + 150);
        Assert.Equal(flat[3, 3], flat[30, 30]);
        Assert.NotEqual(GroundLayer.Color, flat[3, 3]);
    }

    [Fact]
    public void TintsByHeight()
    {
        RgbImage image = Render(new CityTerrain(TestTerrain.Create((x, z) => 0, (x, z) => x < 0 ? 10 : 200)), new HillshadeLayer());

        // Low ground is green, the highest is the palette's white, both lit like flat ground away from the step.
        Assert.True(image[4, 18].G > image[4, 18].R);
        Assert.True(Brightness(image[32, 18]) > Brightness(image[4, 18]));
    }

    [Fact]
    public void DrawsWaterDeepEnoughToSee()
    {
        // 5 m deep west of x = −24, 0.2 m deep from x = 24 on, dry in between.
        WaterSectionContent content = TestTerrain.CreateWater(40, (x, z) => 30, (x, z) => x <= -24 ? 5 : x >= 24 ? 0.2 : 0);
        var water = new CityWater(content, []);

        RgbImage image = Render(water, new WaterLayer());

        Assert.True(image[2, 18].B > image[2, 18].R + 50);
        Assert.Equal(GroundLayer.Color, image[18, 18]);
        Assert.Equal(GroundLayer.Color, image[34, 18]);
    }

    [Fact]
    public void DrawsWaterSources()
    {
        WaterSectionContent content = TestTerrain.CreateWater(40, (x, z) => 30, (x, z) => 0);
        WaterSourceRecord[] sources =
        [
            new() { Id = 1, Type = WaterSourceRecord.NaturalType, InputX = 2, InputZ = 2, OutputX = 2, OutputZ = 2 },
            new() { Id = 2, Type = WaterSourceRecord.FacilityType, InputX = -50, InputZ = -50, OutputX = 50, OutputZ = -50 },
        ];

        RgbImage image = Render(new CityWater(content, sources), new WaterLayer());

        Assert.Equal(WaterLayer.NaturalSourceColor, image[18, 17]);
        Assert.Equal(WaterLayer.BuildingSourceColor, image[5, 30]);
        Assert.Equal(WaterLayer.BuildingSourceColor, image[30, 30]);
    }

    private static RgbImage Render(ISectionData data, IRenderLayer layer)
    {
        var world = new CityWorld("dump", SyntheticDump.CreateManifest(), [data]);
        return TopDownRenderer.Render(world, AreaOfInterest.Plan(world), _view, [new GroundLayer(), layer]);
    }

    private static int Brightness(Rgb color) => color.R + color.G + color.B;
}
