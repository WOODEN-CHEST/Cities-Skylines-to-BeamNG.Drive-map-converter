using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Networks;

public sealed class NetCategoriesTests
{
    [Theory]
    [InlineData("Level1", false, NetCategory.Local)]
    [InlineData("Level2", false, NetCategory.Local)]
    [InlineData("Level3", false, NetCategory.Arterial)]
    [InlineData("Level4", false, NetCategory.Arterial)]
    [InlineData("Level5", true, NetCategory.Highway)]
    [InlineData("Level2", true, NetCategory.Highway)]
    public void SortsRoadsByHighwayRulesAndLevel(string level, bool highway, NetCategory expected) =>
        Assert.Equal(expected, NetCategories.Of(TestNetwork.Road(1, "Road", level: level, highway: highway)));

    [Fact]
    public void SortsOtherNetworksByWhatUsesTheirLanes()
    {
        NetPrefabLane train = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, NetPrefabLane.TrainVehicles, NetPrefabLane.ForwardDirection);
        NetPrefabLane metro = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, NetPrefabLane.MetroVehicles, NetPrefabLane.ForwardDirection);
        NetPrefabLane tram = TestNetwork.Lane(0, NetPrefabLane.TransportVehicleLane, NetPrefabLane.TramVehicles, NetPrefabLane.ForwardDirection);
        NetPrefabLane walk = TestNetwork.Lane(0, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections);
        NetPrefabLane cycle = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, NetPrefabLane.BicycleVehicles, NetPrefabLane.ForwardDirection);
        NetPrefabLane ship = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, 0x8, NetPrefabLane.BothDirections);
        NetPrefabLane car = TestNetwork.Lane(0, NetPrefabLane.VehicleLane, NetPrefabLane.CarVehicles, NetPrefabLane.ForwardDirection);

        Assert.Equal(NetCategory.Rail, NetCategories.Of(TestNetwork.Other(1, "Train Track", "PublicTransport", [train])));
        Assert.Equal(NetCategory.Rail, NetCategories.Of(TestNetwork.Other(1, "Metro Track", "PublicTransport", [metro], layer: NetPrefabRecord.MetroTunnelsLayer)));
        Assert.Equal(NetCategory.Rail, NetCategories.Of(TestNetwork.Other(1, "Tram Track", "PublicTransport", [tram, walk])));
        NetPrefabRecord tramAvenue = TestNetwork.Other(1, "Tram Avenue", "PublicTransport", [car, tram]);
        tramAvenue.Level = "Level3";
        Assert.Equal(NetCategory.Arterial, NetCategories.Of(tramAvenue));
        Assert.Equal(NetCategory.Path, NetCategories.Of(TestNetwork.Other(1, "Pedestrian Path", "Beautification", [walk])));
        Assert.Equal(NetCategory.Path, NetCategories.Of(TestNetwork.Other(1, "Bicycle Path", "Beautification", [cycle])));
        Assert.Equal(NetCategory.Other, NetCategories.Of(TestNetwork.Other(1, "Water Pipe", "Water", [], layer: NetPrefabRecord.WaterPipesLayer)));
        Assert.Equal(NetCategory.Other, NetCategories.Of(TestNetwork.Other(1, "Power Line", "Electricity", [])));
        Assert.Equal(NetCategory.Other, NetCategories.Of(TestNetwork.Other(1, "Ship Path", "PublicTransport", [ship])));
    }

    [Fact]
    public void LeavesOutTransportLinesAndRoutesOnLayersOfTheirOwn()
    {
        NetPrefabLane walk = TestNetwork.Lane(0, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections);
        NetPrefabLane bus = TestNetwork.Lane(0, NetPrefabLane.PublicTransportLane, 0, NetPrefabLane.ForwardDirection);
        NetPrefabRecord busLine = TestNetwork.Other(1, "Bus Line", "PublicTransport", [bus], layer: NetPrefabRecord.PublicTransportLayer);
        NetPrefabRecord busLineOnTheDefaultLayer = TestNetwork.Other(1, "Bus Line", "Road", [bus]);
        busLineOnTheDefaultLayer.AiType = NetCategories.TransportLineAiType;

        // Ship connection paths have a pedestrian lane, but lie on the ship paths' layer (0x20).
        Assert.Equal(NetCategory.Other, NetCategories.Of(TestNetwork.Other(1, "Ship Connection Path", "PublicTransport", [walk], layer: 0x20)));
        Assert.Equal(NetCategory.Other, NetCategories.Of(busLine));
        Assert.Equal(NetCategory.Other, NetCategories.Of(busLineOnTheDefaultLayer));
    }
}
