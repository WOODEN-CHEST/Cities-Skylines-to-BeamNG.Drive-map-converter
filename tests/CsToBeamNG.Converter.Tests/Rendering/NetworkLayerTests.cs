using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.Rendering.Layers;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class NetworkLayerTests
{
    // 200 m square at 1 m per pixel: pixel (i, j) covers x −100 + i to −99 + i and z 99 − j to 100 − j.
    private static readonly RenderView _view = new(new MapRect(-100, -100, 100, 100), 200);

    [Fact]
    public void DrawsSegmentsAsBandsOfTheirWidthWithACasing()
    {
        RgbImage image = Render(TestCity(), _view);

        // The basic road along z = 0 is 16 m wide: rows 92 to 107, with a casing one pixel wide around it.
        Assert.Equal(NetworkLayer.LocalColor, image[100, 92]);
        Assert.Equal(NetworkLayer.LocalColor, image[100, 107]);
        Assert.Equal(NetworkLayer.CasingColor, image[100, 91]);
        Assert.Equal(NetworkLayer.CasingColor, image[100, 108]);
        Assert.Equal(GroundLayer.Color, image[100, 89]);

        // The highway along x = 50.25 crosses it on top, and is 18 m wide: columns 141 to 158.
        Assert.Equal(NetworkLayer.HighwayColor, image[145, 60]);
        Assert.Equal(NetworkLayer.HighwayColor, image[156, 100]);
        Assert.Equal(NetworkLayer.CasingColor, image[159, 60]);
    }

    [Fact]
    public void DrawsTunnelsFaintlyAndLeavesOtherNetworksOut()
    {
        RgbImage image = Render(TestCity(), _view);

        Assert.Equal(GroundLayer.Color.Blend(NetworkLayer.LocalColor, NetworkLayer.TunnelOpacity), image[50, 40]);
        Assert.Equal(GroundLayer.Color, image[100, 160]);
    }

    [Fact]
    public void MarksOneWaySegmentsAndTheCornersAtSegmentEnds()
    {
        RgbImage image = Render(TestCity(), _view);

        // The highway goes north: its arrow's tip lies north of the middle, at (50.25, 4.25), the ends of its arms south
        // of it, at (45.45, −3.75) and (55.05, −3.75). (Lines fill the pixel that holds a point, so the test keeps points
        // off pixel edges.)
        Assert.Equal(NetworkLayer.MarkColor, image[150, 95]);
        Assert.Equal(NetworkLayer.MarkColor, image[145, 103]);
        Assert.Equal(NetworkLayer.MarkColor, image[155, 103]);
        Assert.Equal(NetworkLayer.HighwayColor, image[150, 105]);

        // The basic road starts at x = −80, where the line between its start corners runs across it.
        Assert.Equal(NetworkLayer.MarkColor, image[20, 100]);
        Assert.Equal(NetworkLayer.LocalColor, image[30, 100]);
    }

    [Fact]
    public void LeavesOutDetailsWhenZoomedOut()
    {
        // 800 m at 200 pixels: 4 m per pixel. The 1 m wide path becomes a line, and there are no corner lines or arrows.
        RgbImage image = Render(TestCity(), new RenderView(new MapRect(-400, -400, 400, 400), 200));

        Assert.Equal(NetworkLayer.PathColor, image[95, 92]);
        Assert.Equal(NetworkLayer.LocalColor, image[80, 100]);
        Assert.Equal(NetworkLayer.HighwayColor, image[112, 100]);
    }

    [Fact]
    public void DrawsSegmentsWithoutPrefabsAsLines()
    {
        TestNetwork network = Network();
        var world = new CityWorld("dump", SyntheticDump.CreateManifest(), [new CityNetSegments([.. network.Segments.Segments], null)]);

        RgbImage image = TopDownRenderer.Render(world, AreaOfInterest.Plan(world), _view, [new GroundLayer(), new NetworkLayer()]);

        Assert.Equal(NetworkLayer.UnknownColor, image[100, 100]);
        Assert.Equal(GroundLayer.Color, image[100, 92]);
    }

    [Fact]
    public void HasAColourForEveryDrawnCategory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkLayer.ColorOf(NetCategory.Other));
        Assert.Equal(NetworkLayer.RailColor, NetworkLayer.ColorOf(NetCategory.Rail));
        Assert.Equal(NetworkLayer.ArterialColor, NetworkLayer.ColorOf(NetCategory.Arterial));
    }

    // A basic road along z = 0 from x = −80 to 80; a one-way highway north along x = 50.25, from z = −79.75 to 80.25;
    // a road tunnel along z = 60 from x = −80 to −20; a pipe along z = −60; a 1 m wide path along z = 30.
    private static TestNetwork Network()
    {
        NetPrefabRecord tunnel = TestNetwork.Road(3, "Basic Road Tunnel");
        tunnel.Underground = true;
        var network = new TestNetwork()
            .AddPrefab(TestNetwork.Road(1, "Basic Road"))
            .AddPrefab(TestNetwork.Road(2, "Highway", level: "Level5", halfWidth: 9, highway: true, oneWay: true))
            .AddPrefab(tunnel)
            .AddPrefab(TestNetwork.Other(4, "Water Pipe", "Water", [], layer: NetPrefabRecord.WaterPipesLayer))
            .AddPrefab(TestNetwork.Other(5, "Footpath", "Beautification", [TestNetwork.Lane(0, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections)], halfWidth: 0.5f));
        network.AddNode(1, -80, 0, NetNodeRecord.EndFlag);
        network.AddNode(2, 80, 0, NetNodeRecord.EndFlag);
        network.AddNode(3, 50.25f, -79.75f, NetNodeRecord.EndFlag, prefab: 2);
        network.AddNode(4, 50.25f, 80.25f, NetNodeRecord.EndFlag, prefab: 2);
        network.AddNode(5, -80, 60, NetNodeRecord.EndFlag | NetNodeRecord.UndergroundFlag, prefab: 3);
        network.AddNode(6, -20, 60, NetNodeRecord.EndFlag | NetNodeRecord.UndergroundFlag, prefab: 3);
        network.AddNode(7, -80, -60, NetNodeRecord.EndFlag, prefab: 4);
        network.AddNode(8, 80, -60, NetNodeRecord.EndFlag, prefab: 4);
        network.AddNode(9, -60, 30, NetNodeRecord.EndFlag, prefab: 5);
        network.AddNode(10, 0, 30, NetNodeRecord.EndFlag, prefab: 5);
        network.AddStraightSegment(1, 1, 1, 2);
        network.AddStraightSegment(2, 2, 3, 4);
        network.AddStraightSegment(3, 3, 5, 6);
        network.AddStraightSegment(4, 4, 7, 8);
        network.AddStraightSegment(5, 5, 9, 10);
        return network;
    }

    private static CityWorld TestCity()
    {
        TestNetwork network = Network();
        var prefabs = new CityNetPrefabs([.. network.Prefabs.Prefabs]);
        return new CityWorld(
            "dump",
            SyntheticDump.CreateManifest(),
            [prefabs, new CityNetNodes([.. network.Nodes.Nodes], []), new CityNetSegments([.. network.Segments.Segments], prefabs)]);
    }

    private static RgbImage Render(CityWorld world, RenderView view) =>
        TopDownRenderer.Render(world, AreaOfInterest.Plan(world), view, [new GroundLayer(), new NetworkLayer()]);
}
