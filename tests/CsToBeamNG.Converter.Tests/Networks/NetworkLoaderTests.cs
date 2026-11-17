using System;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Networks;

public sealed class NetworkLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LoadsTheNetworkSections()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = SmallCity().AddTo(new SyntheticDump().WithUnlockedTiles(new TileCoordinate(4, 4))).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.All(result.Sections, section => Assert.Equal(SectionLoadStatus.Loaded, section.Status));
        Assert.Equal(["net-prefabs", "net-nodes", "net-segments", "net-lanes"], result.Sections.Select(section => section.Section.Name));
        Assert.Empty(result.Messages);
        Assert.Equal(["terrain", "water", "building-prefabs", "buildings", "prop-prefabs", "props", "tree-prefabs", "trees"], result.MissingSections);
        CityNetPrefabs prefabs = Assert.IsType<CityNetPrefabs>(result.World.Find<CityNetPrefabs>());
        CityNetNodes nodes = Assert.IsType<CityNetNodes>(result.World.Find<CityNetNodes>());
        CityNetSegments segments = Assert.IsType<CityNetSegments>(result.World.Find<CityNetSegments>());
        CityNetLanes lanes = Assert.IsType<CityNetLanes>(result.World.Find<CityNetLanes>());
        Assert.Equal(
            [NetCategory.Local, NetCategory.Arterial, NetCategory.Highway, NetCategory.Rail, NetCategory.Path, NetCategory.Other],
            prefabs.Prefabs.Select(prefab => prefabs.CategoryOf(prefab.Index)));
        Assert.Equal(NetCategory.Other, prefabs.CategoryOf(99));
        Assert.True(nodes.TryGet(4, out NetNodeRecord? outside));
        Assert.True(outside.HasFlags(NetNodeRecord.OutsideFlag));
        Assert.True(segments.TryGet(11, out NetSegmentRecord? highway));
        Assert.Equal(3u, highway.Prefab);
        Assert.True(lanes.TryGet(102, out NetLaneRecord? lane));
        Assert.True(lanes.TryGetPrefabLane(lane, out NetPrefabLane? settings));
        Assert.Equal(NetPrefabLane.ForwardDirection, settings.Direction);
        Assert.False(lanes.TryGetPrefabLane(new NetLaneRecord { Segment = 77 }, out _));

        // The outside connection lies beyond the unlocked tile, so only the city's own nodes bound the area of interest.
        Assert.Equal(new MapRect(0, 0, 100, 100), nodes.AreaOfInterestBounds);
        Assert.Equal(3, nodes.ContentNodes);
        AreaOfInterest area = AreaOfInterest.Plan(result.World);
        Assert.Equal(AreaOfInterestSource.Content, area.Source);
        Assert.Equal(new MapRect(-500, -500, 600, 600), area.Bounds);
    }

    [Fact]
    public void ReportsStatistics()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = SmallCity().AddTo(new SyntheticDump().WithUnlockedTiles(new TileCoordinate(4, 4))).WriteTo(_temp.PathOf("dump"));

        CityWorld world = WorldLoader.Load(folder).World;

        Assert.Equal(
            [("prefabs", "6, with 15 lanes"), ("by category", "1 highway, 1 arterial, 1 local, 1 path, 1 rail, 1 other")],
            Statistics(world.Find<CityNetPrefabs>()!));
        Assert.Equal(
            [
                ("nodes", "4: 1 junctions, 0 bends, 2 ends, 1 middle nodes"),
                ("traffic lights", "at 1 nodes"),
                ("outside connections", "1"),
                ("off the ground", "0 elevated, 0 underground"),
                ("content", "3 nodes within x 0 to 100, z 0 to 100"),
            ],
            Statistics(world.Find<CityNetNodes>()!));
        Assert.Equal(
            [
                ("segments", "3, 2.16 km of centre lines"),
                ("by category", "highway 0.1 km, arterial 0 km, local 0.1 km, path 0 km, rail 0 km, other 1.96 km"),
                ("one-way", "1 segments"),
            ],
            Statistics(world.Find<CityNetSegments>()!));
        Assert.Equal(
            [("lanes", "4, 0.4 km by the game's lengths"), ("by type", "2 for vehicles, 2 for pedestrians, 0 for parking, 0 other")],
            Statistics(world.Find<CityNetLanes>()!));
        Assert.All(world.Sections.Where(data => data is not CityNetNodes), data => Assert.Null(data.AreaOfInterestBounds));
    }

    [Fact]
    public void ReportsReferencesToMissingObjectsAndKeepsTheObjects()
    {
        TestNetwork network = SmallCity();
        network.Segments.Segments.Add(new NetSegmentRecord { Id = 50, Prefab = 42, StartNode = 1, EndNode = 77, Bezier = Straight(0, 0, 10, 0) });
        network.Lanes.Lanes.Add(new NetLaneRecord { Id = 900, Segment = 88, Bezier = Straight(0, 0, 10, 0) });
        network.Lanes.Lanes.Add(new NetLaneRecord { Id = 901, Segment = 10, Index = 9, Bezier = Straight(0, 0, 10, 0) });
        network.Nodes.Nodes.Add(new NetNodeRecord { Id = 60, Prefab = 43, Position = new DumpVector3(5, 60, 5) });
        string folder = network.AddTo(new SyntheticDump()).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Equal(
            [
                "1 nodes have a prefab that the net-prefabs section lacks, such as node 60 (prefab 43); they are kept.",
                "1 segments have a prefab that the net-prefabs section lacks, such as segment 50 (prefab 42); they are kept.",
                "1 segments end at nodes that the net-nodes section lacks, such as segment 50 (nodes 1 and 77); they are kept.",
                "1 lanes belong to segments that the net-segments section lacks, such as lane 900 (segment 88); they are kept.",
                "1 lanes have an index beyond their prefab's lanes, such as lane 901 (index 9 of 4 in \"Basic Road\"); they are kept.",
            ],
            result.Messages.Select(message => message.Message));
        Assert.All(result.Messages, message => Assert.Equal(LoadMessageSeverity.Warning, message.Severity));
        Assert.True(result.World.Find<CityNetSegments>()!.TryGet(50, out _));
        Assert.Equal(6, result.World.Find<CityNetLanes>()!.Lanes.Count);

        // Without unlocked tiles, every node bounds the area of interest.
        Assert.Equal(new MapRect(-500, 0, 100, 2000), result.World.Find<CityNetNodes>()!.AreaOfInterestBounds);
    }

    [Fact]
    public void SkipsObjectsWithValuesThatArentFiniteAndDuplicateIds()
    {
        TestNetwork network = SmallCity();
        network.Segments.Segments.Add(new NetSegmentRecord { Id = 51, Prefab = 1, StartNode = 1, EndNode = 2, Bezier = Straight(0, 0, 10, 0), AverageLength = float.NaN });
        network.Lanes.Lanes.Add(new NetLaneRecord { Id = 905, Segment = 10, Bezier = Straight(0, float.PositiveInfinity, 10, 0) });
        string folder = new SyntheticDump()
            .WithNetwork(network.Prefabs, null, network.Segments, network.Lanes)
            .WithRawNodes((1, 0f), (2, 100f), (2, 5f), (3, float.NaN))
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        // Messages come in the manifest's order of the sections; the raw nodes section was written last.
        string[] messages = [.. result.Messages.Select(message => message.Message)];
        Assert.Contains("Node 2 is listed twice, so its second record is skipped.", messages);
        Assert.Contains("Node 3 has a position that isn't finite, so it is skipped.", messages);
        Assert.Contains("Segment 51 has a position, direction or length that isn't finite, so it is skipped.", messages);
        Assert.Contains("Lane 905 has a curve or length that isn't finite, so it is skipped.", messages);
        Assert.Contains("2 segments end at nodes that the net-nodes section lacks, such as segment 11 (nodes 1 and 3); they are kept.", messages);
        Assert.Equal(5, messages.Length);
        CityNetNodes nodes = result.World.Find<CityNetNodes>()!;
        Assert.Equal([1u, 2u], nodes.Nodes.Select(node => node.Id));
        Assert.Equal(100f, nodes.Nodes[1].Position.X);
        Assert.False(result.World.Find<CityNetSegments>()!.TryGet(51, out _));
    }

    [Fact]
    public void DescribesSegmentsWithoutPrefabs()
    {
        TestNetwork network = SmallCity();
        string folder = new SyntheticDump().WithNetwork(null, network.Nodes, network.Segments, null).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Empty(result.Messages);
        Assert.Equal(["building-prefabs", "buildings", "net-lanes", "net-prefabs", "prop-prefabs", "props", "terrain", "tree-prefabs", "trees", "water"], result.MissingSections.Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                ("segments", "3, 2.16 km of centre lines"),
                ("by category", "unknown without the net-prefabs section"),
                ("one-way", "unknown without the net-prefabs section"),
            ],
            Statistics(result.World.Find<CityNetSegments>()!));
    }

    [Fact]
    public void RejectsDuplicatesInTheWorldModel()
    {
        Assert.Throws<ArgumentException>(() => new CityNetPrefabs([TestNetwork.Road(1, "A"), TestNetwork.Road(1, "B")]));
        Assert.Throws<ArgumentException>(() => new CityNetNodes([new NetNodeRecord { Id = 1 }, new NetNodeRecord { Id = 1 }], []));
        Assert.Throws<ArgumentException>(() => new CityNetSegments([new NetSegmentRecord { Id = 1 }, new NetSegmentRecord { Id = 1 }], null));
        Assert.Throws<ArgumentException>(() => new CityNetLanes([new NetLaneRecord { Id = 1 }, new NetLaneRecord { Id = 1 }], null, null));
        Assert.Null(new CityNetNodes([], []).AreaOfInterestBounds);
    }

    /// <summary>
    /// A junction with traffic lights at the origin, a basic road east to a dead end, a one-way highway north to a middle
    /// node, a pipe from there to an outside connection far beyond tile (4, 4), and lanes on the basic road.
    /// </summary>
    internal static TestNetwork SmallCity()
    {
        var network = new TestNetwork()
            .AddPrefab(TestNetwork.Road(1, "Basic Road"))
            .AddPrefab(TestNetwork.Road(2, "Medium Road", level: "Level3", halfWidth: 12))
            .AddPrefab(TestNetwork.Road(3, "Highway", level: "Level5", halfWidth: 9, highway: true, oneWay: true))
            .AddPrefab(TestNetwork.Other(4, "Train Track", "PublicTransport", [TestNetwork.Lane(-2, NetPrefabLane.VehicleLane, NetPrefabLane.TrainVehicles, NetPrefabLane.ForwardDirection), TestNetwork.Lane(2, NetPrefabLane.VehicleLane, NetPrefabLane.TrainVehicles, NetPrefabLane.BackwardDirection)]))
            .AddPrefab(TestNetwork.Other(5, "Pedestrian Path", "Beautification", [TestNetwork.Lane(0, NetPrefabLane.PedestrianLane, 0, NetPrefabLane.BothDirections)], halfWidth: 2))
            .AddPrefab(TestNetwork.Other(6, "Water Pipe", "Water", [], layer: NetPrefabRecord.WaterPipesLayer));
        network.AddNode(1, 0, 0, NetNodeRecord.JunctionFlag | NetNodeRecord.TrafficLightsFlag);
        network.AddNode(2, 100, 0, NetNodeRecord.EndFlag);
        network.AddNode(3, 0, 100, NetNodeRecord.MiddleFlag, prefab: 3);
        network.AddNode(4, -500, 2000, NetNodeRecord.EndFlag | NetNodeRecord.OutsideFlag, prefab: 6);
        network.AddStraightSegment(10, 1, 1, 2, firstLaneId: 100);
        network.AddStraightSegment(11, 3, 1, 3);
        network.AddStraightSegment(12, 6, 3, 4);
        return network;
    }

    private static DumpBezier3 Straight(float x0, float z0, float x1, float z1) => new(
        new DumpVector3(x0, 60, z0),
        new DumpVector3(x0 + ((x1 - x0) / 3), 60, z0 + ((z1 - z0) / 3)),
        new DumpVector3(x0 + (2 * (x1 - x0) / 3), 60, z0 + (2 * (z1 - z0) / 3)),
        new DumpVector3(x1, 60, z1));

    private static (string Name, string Value)[] Statistics(ISectionData data) => [.. data.Statistics.Select(s => (s.Name, s.Value))];
}

/// <summary>Writes node records directly, bypassing the section writer's checks, to test how loaders treat bad data.</summary>
internal static class RawNetworkSections
{
    public static SyntheticDump WithRawNodes(this SyntheticDump dump, params (uint Id, float X)[] nodes) =>
        dump.WithCustomSection(writer =>
        {
            SectionWriter section = writer.BeginSection(NetNodesSection.Name, NetNodesSection.Version);
            using (RecordFileWriter records = section.CreateRecordFile(NetNodesSection.NodesFileName, NetNodesSection.NodeRecordType, NetNodesSection.NodeRecordSize))
            {
                foreach ((uint id, float x) in nodes)
                {
                    records.BeginRecord();
                    records.WriteUInt32(id);
                    records.WriteUInt32(NetNodeRecord.CreatedFlag);
                    records.WriteUInt32(0);
                    records.WriteUInt32(1);
                    records.WriteSingle(x);
                    records.WriteSingle(60);
                    records.WriteSingle(0);
                    records.WriteZeros(NetNodesSection.NodeRecordSize - 28);
                    records.EndRecord();
                }

                records.Finish();
            }

            section.RecordCount = nodes.Length;
            section.Complete();
        });
}
