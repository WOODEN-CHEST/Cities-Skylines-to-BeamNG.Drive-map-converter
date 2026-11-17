using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Buildings;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Props;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.Rendering.Layers;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.Trees;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class ObjectLayerTests
{
    // 100 m square at 1 m per pixel: pixel (i, j) covers x −50 + i to −49 + i and z 49 − j to 50 − j. Objects stand half a
    // metre off the grid lines, so their edges fall inside pixels.
    private static readonly RenderView _view = new(new MapRect(-50, -50, 50, 50), 100);

    [Fact]
    public void DrawsLotsInTheirZonesColourWithTheFrontEdgeDark()
    {
        var objects = new TestObjects().Add(TestObjects.BuildingPrefab(10, "Shop", "Commercial", 2, 3));
        objects.AddBuilding(1, 10, 0.5f, 0.5f, width: 2, length: 3);

        RgbImage image = Render(objects, new BuildingLayer());

        // The lot spans x −7.5 to 8.5 and z −11.5 to 12.5, its front facing north at z = 12.5 (row 37).
        Assert.Equal(BuildingLayer.CommercialColor, image[50, 50]);
        Assert.Equal(BuildingLayer.FrontColor, image[50, 37]);
        Assert.Equal(BuildingLayer.OutlineColor, image[50, 61]);
        Assert.Equal(BuildingLayer.OutlineColor, image[42, 50]);
        Assert.Equal(GroundLayer.Color, image[50, 35]);
        Assert.Equal(GroundLayer.Color, image[60, 50]);
    }

    [Theory]
    [InlineData(Math.PI, 50, 61)]
    [InlineData(Math.PI / 2, 38, 50)]
    [InlineData(-Math.PI / 2, 62, 50)]
    public void TurnsTheFrontWithTheBuilding(double angle, int frontColumn, int frontRow)
    {
        var objects = new TestObjects().Add(TestObjects.BuildingPrefab(10, "Shop", "Commercial", 2, 3));
        objects.AddBuilding(1, 10, 0.5f, 0.5f, angle: (float)angle, width: 2, length: 3);

        RgbImage image = Render(objects, new BuildingLayer());

        // Turned by π the front faces south (z = −11.5); by π/2 west (x = −11.5); by −π/2 east (x = 12.5).
        Assert.Equal(BuildingLayer.FrontColor, image[frontColumn, frontRow]);
        Assert.Equal(BuildingLayer.CommercialColor, image[50, 50]);
    }

    [Fact]
    public void DrawsBuildingsWithoutPrefabsGreyAndLeavesOutThoseTheGameDoesntDraw()
    {
        var objects = new TestObjects();
        objects.AddBuilding(1, 10, 0.5f, 0.5f);
        objects.AddBuilding(2, 10, 30.5f, 30.5f, flags: BuildingRecord.CreatedFlag | BuildingRecord.HiddenFlag);
        var world = new CityWorld("dump", SyntheticDump.CreateManifest(), [new CityBuildings([.. objects.Buildings.Buildings], [], null)]);

        RgbImage image = TopDownRenderer.Render(world, AreaOfInterest.Plan(world), _view, [new GroundLayer(), new BuildingLayer()]);

        Assert.Equal(BuildingLayer.UnknownColor, image[50, 50]);
        Assert.Equal(GroundLayer.Color, image[80, 20]);
    }

    [Fact]
    public void LeavesOutBuildingsOffTheDefaultLayer()
    {
        BuildingPrefabRecord junction = TestObjects.BuildingPrefab(11, "Water Pipe Junction", "Water", 1, 1);
        junction.Class.Layer = 0x2;
        var objects = new TestObjects().Add(TestObjects.BuildingPrefab(10, "Shop", "Commercial")).Add(junction);
        objects.AddBuilding(1, 10, 0.5f, 0.5f);
        objects.AddBuilding(2, 11, 30.5f, 30.5f, width: 1, length: 1);

        RgbImage image = Render(objects, new BuildingLayer());

        Assert.Equal(BuildingLayer.CommercialColor, image[50, 50]);
        Assert.Equal(GroundLayer.Color, image[80, 19]);
    }

    [Fact]
    public void DrawsSmallLotsAsOnePixelWhenZoomedOut()
    {
        var objects = new TestObjects().Add(TestObjects.BuildingPrefab(10, "House", "Residential", 1, 1));
        objects.AddBuilding(1, 10, 2, 2, width: 1, length: 1);

        // 800 m at 100 pixels: 8 m per pixel, so the 8 m lot is one pixel across, below the limit of 1.5.
        RgbImage image = Render(objects, new BuildingLayer(), new RenderView(new MapRect(-400, -400, 400, 400), 100));

        Assert.Equal(BuildingLayer.ResidentialColor, image[50, 49]);
        Assert.Equal(GroundLayer.Color, image[51, 49]);
        Assert.Equal(GroundLayer.Color, image[50, 50]);
    }

    [Fact]
    public void HasAColourForEveryCategory()
    {
        Assert.Equal(BuildingLayer.OfficeColor, BuildingLayer.ColorOf(BuildingCategory.Office));
        Assert.Equal(BuildingLayer.IndustrialColor, BuildingLayer.ColorOf(BuildingCategory.Industrial));
        Assert.Equal(BuildingLayer.OtherColor, BuildingLayer.ColorOf(BuildingCategory.Other));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildingLayer.ColorOf((BuildingCategory)99));
    }

    [Fact]
    public void DrawsTreesAsDiscsOfTheirCrownsOrAsDots()
    {
        var objects = new TestObjects().Add(TestObjects.TreePrefab(30, "Oak", 4));
        objects.AddTree(1, 30, 10.5f, 10.5f, scale: 0.75f);
        objects.AddTree(2, 30, -20.5f, -20.5f, flags: TreeRecord.CreatedFlag);

        RgbImage image = Render(objects, new TreeLayer());
        RgbImage far = Render(objects, new TreeLayer(), new RenderView(new MapRect(-400, -400, 400, 400), 100));

        // The crown reaches 4 × 0.75 = 3 m from the trunk at (10.5, 10.5): columns 58 to 62, rows 37 to 41.
        Assert.Equal(3, TreeLayer.CrownRadiusMeters(objects.Trees.Trees[0], new CityTreePrefabs([.. objects.TreePrefabs.Prefabs])), 6);
        Assert.Equal(TreeLayer.TreeColor, image[60, 39]);
        Assert.Equal(TreeLayer.TreeColor, image[62, 39]);
        Assert.Equal(GroundLayer.Color, image[64, 39]);

        // The tree with grow state 0 isn't drawn; zoomed out, the crown is under 1.5 pixels and becomes one pixel.
        Assert.Equal(GroundLayer.Color, image[29, 70]);
        Assert.Equal(TreeLayer.TreeColor, far[51, 48]);
        Assert.Equal(GroundLayer.Color, far[52, 48]);
    }

    [Fact]
    public void GuessesTheCrownOfATreeWithoutAPrefab()
    {
        var tree = new TreeRecord { Scale = 2 };

        Assert.Equal(2 * TreeLayer.UnknownCrownRadiusMeters, TreeLayer.CrownRadiusMeters(tree, null));
        Assert.Equal(2 * TreeLayer.UnknownCrownRadiusMeters, TreeLayer.CrownRadiusMeters(tree, new CityTreePrefabs([])));
    }

    [Theory]
    [InlineData(0, 30, 22, 29, 21)]
    [InlineData(Math.PI / 2, 29, 21, 30, 22)]
    public void DrawsPropsAsTheirFootprintsTurnedByTheirAngles(double angle, int insideColumn, int insideRow, int outsideColumn, int outsideRow)
    {
        // A bench 1 m wide and 4 m long along its local Z, at (−19.5, 28.5): unturned it covers x −20 to −19 and z 26.5 to
        // 30.5; turned by π/2, x −21.5 to −17.5 and z 28 to 29.
        var objects = new TestObjects().Add(TestObjects.PropPrefab(20, "Bench", new DumpVector3(-0.5f, 0, -2), new DumpVector3(0.5f, 1, 2)));
        objects.AddProp(1, 20, -19.5f, 28.5f, angle: (float)angle);

        RgbImage image = Render(objects, new PropLayer());

        Assert.Equal(PropLayer.PropColor, image[30, 21]);
        Assert.Equal(PropLayer.PropColor, image[insideColumn, insideRow]);
        Assert.Equal(GroundLayer.Color, image[outsideColumn, outsideRow]);
    }

    [Fact]
    public void DrawsDecalsPaleAndLeavesOutMarkersAndHiddenProps()
    {
        var objects = new TestObjects()
            .Add(TestObjects.PropPrefab(20, "Bench", new DumpVector3(-0.5f, 0, -0.5f), new DumpVector3(0.5f, 1, 0.5f)))
            .Add(TestObjects.PropPrefab(21, "Decal", new DumpVector3(-2, 0, -2), new DumpVector3(2, 0.1f, 2), decal: true))
            .Add(TestObjects.PropPrefab(22, "Marker", new DumpVector3(-2, 0, -2), new DumpVector3(2, 1, 2), marker: true));
        objects.AddProp(1, 21, 0.5f, 0.5f);
        objects.AddProp(2, 22, 20.5f, 0.5f);
        objects.AddProp(3, 20, -20.5f, 0.5f, flags: PropRecord.CreatedFlag | PropRecord.HiddenFlag);
        objects.AddProp(4, 20, 30.5f, 30.5f);

        RgbImage image = Render(objects, new PropLayer());

        Assert.Equal(GroundLayer.Color.Blend(PropLayer.DecalColor, PropLayer.DecalOpacity), image[50, 49]);
        Assert.Equal(GroundLayer.Color, image[70, 49]);
        Assert.Equal(GroundLayer.Color, image[29, 49]);

        // The 1 m bench is under 2 pixels across, so it is one pixel.
        Assert.Equal(PropLayer.PropColor, image[80, 19]);
        Assert.Equal(GroundLayer.Color, image[81, 19]);
    }

    private static RgbImage Render(TestObjects objects, IRenderLayer layer, RenderView? view = null)
    {
        var buildingPrefabs = new CityBuildingPrefabs([.. objects.BuildingPrefabs.Prefabs]);
        var propPrefabs = new CityPropPrefabs([.. objects.PropPrefabs.Prefabs]);
        var sections = new List<ISectionData>
        {
            buildingPrefabs,
            new CityBuildings([.. objects.Buildings.Buildings], [], buildingPrefabs),
            propPrefabs,
            new CityProps([.. objects.Props.Props], propPrefabs),
            new CityTreePrefabs([.. objects.TreePrefabs.Prefabs]),
            new CityTrees([.. objects.Trees.Trees]),
        };
        var world = new CityWorld("dump", SyntheticDump.CreateManifest(), sections);
        return TopDownRenderer.Render(world, AreaOfInterest.Plan(world), view ?? _view, [new GroundLayer(), layer]);
    }
}
