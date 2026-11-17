using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Dump.Tests.Sections;

public sealed class NetworkSectionsTests : IDisposable
{
    private const int HeaderSize = 64;

    private readonly TempFolder _temp = new();
    private int _dumps;

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsNodes(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new NetNodesSectionContent();
        content.Nodes.Add(Node(9, 0.5f));
        content.Nodes.Add(Node(3, -1.25f));

        DumpSection section = WriteAndOpen(dump => NetNodesSectionWriter.Write(dump, content), NetNodesSection.Name);
        NetNodesSectionContent read = NetNodesSectionReader.Read(section);

        Assert.Equal(NetNodesSection.Version, section.Version);
        Assert.Equal(2, section.RecordCount);
        Assert.Equal([3u, 9u], read.Nodes.Select(node => node.Id));
        NetNodeRecord node = read.Nodes[1];
        NetNodeRecord expected = Node(9, 0.5f);
        Assert.Equal(expected.Flags, node.Flags);
        Assert.Equal(expected.Flags2, node.Flags2);
        Assert.Equal(expected.Prefab, node.Prefab);
        Assert.Equal(expected.Position, node.Position);
        Assert.Equal(expected.Building, node.Building);
        Assert.Equal(expected.Segments, node.Segments);
        Assert.Equal((expected.Elevation, expected.HeightOffset), (node.Elevation, node.HeightOffset));
        Assert.Equal(HeaderSize + (2 * 68), new FileInfo(section.GetFilePath(NetNodesSection.NodesFileName)).Length);
    }

    [Fact]
    public void WritesNodesWithTheDocumentedLayout()
    {
        var content = new NetNodesSectionContent();
        content.Nodes.Add(Node(9, 0.5f));

        byte[] record = OnlyRecord(WriteAndOpen(dump => NetNodesSectionWriter.Write(dump, content), NetNodesSection.Name), NetNodesSection.NodesFileName, NetNodesSection.NodeRecordSize);

        Assert.Equal(9u, UInt32At(record, 0));
        Assert.Equal(0x80800081u, UInt32At(record, 4));
        Assert.Equal(0x24u, UInt32At(record, 8));
        Assert.Equal(47u, UInt32At(record, 12));
        Assert.Equal(-1200.5f, SingleAt(record, 16));
        Assert.Equal(60.5f, SingleAt(record, 20));
        Assert.Equal(812.25f, SingleAt(record, 24));
        Assert.Equal(77u, UInt32At(record, 28));
        Assert.Equal(11u, UInt32At(record, 32));
        Assert.Equal(65535u, UInt32At(record, 60));
        Assert.Equal(12, record[64]);
        Assert.Equal(3, record[65]);
        Assert.Equal(0, record[66] | record[67]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsSegments(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new NetSegmentsSectionContent();
        content.Segments.Add(Segment(20));
        content.Segments.Add(Segment(4));

        DumpSection section = WriteAndOpen(dump => NetSegmentsSectionWriter.Write(dump, content), NetSegmentsSection.Name);
        NetSegmentsSectionContent read = NetSegmentsSectionReader.Read(section);

        Assert.Equal(NetSegmentsSection.Version, section.Version);
        Assert.Equal(2, section.RecordCount);
        Assert.Equal([4u, 20u], read.Segments.Select(segment => segment.Id));
        NetSegmentRecord segment = read.Segments[1];
        NetSegmentRecord expected = Segment(20);
        Assert.Equal(
            (expected.Flags, expected.Flags2, expected.Prefab, expected.StartNode, expected.EndNode),
            (segment.Flags, segment.Flags2, segment.Prefab, segment.StartNode, segment.EndNode));
        Assert.Equal(expected.StartDirection, segment.StartDirection);
        Assert.Equal(expected.EndDirection, segment.EndDirection);
        Assert.Equal(expected.MiddlePosition, segment.MiddlePosition);
        Assert.Equal(expected.Bezier, segment.Bezier);
        Assert.Equal(expected.StartLeft, segment.StartLeft);
        Assert.Equal(expected.StartRight, segment.StartRight);
        Assert.Equal(expected.EndLeft, segment.EndLeft);
        Assert.Equal(expected.EndRight, segment.EndRight);
        Assert.Equal(expected.AverageLength, segment.AverageLength);
        Assert.Equal(HeaderSize + (2 * 212), new FileInfo(section.GetFilePath(NetSegmentsSection.SegmentsFileName)).Length);
    }

    [Fact]
    public void WritesSegmentsWithTheDocumentedLayout()
    {
        var content = new NetSegmentsSectionContent();
        content.Segments.Add(Segment(20));

        byte[] record = OnlyRecord(WriteAndOpen(dump => NetSegmentsSectionWriter.Write(dump, content), NetSegmentsSection.Name), NetSegmentsSection.SegmentsFileName, NetSegmentsSection.SegmentRecordSize);

        Assert.Equal(20u, UInt32At(record, 0));
        Assert.Equal(0x10u, UInt32At(record, 4));
        Assert.Equal(0x400u, UInt32At(record, 8));
        Assert.Equal(12u, UInt32At(record, 12));
        Assert.Equal((5u, 6u), (UInt32At(record, 16), UInt32At(record, 20)));
        Assert.Equal(1f, SingleAt(record, 24));
        Assert.Equal(-1f, SingleAt(record, 44));
        Assert.Equal(50f, SingleAt(record, 48));
        Assert.Equal(0f, SingleAt(record, 60));
        Assert.Equal(100f, SingleAt(record, 96));
        Assert.Equal((0.25f, -8f), (SingleAt(record, 108), SingleAt(record, 116)));
        Assert.Equal(1f, SingleAt(record, 120));
        Assert.Equal(8f, SingleAt(record, 140));
        Assert.Equal(-8f, SingleAt(record, 188));
        Assert.Equal(-1f, SingleAt(record, 192));
        Assert.Equal(99.5f, SingleAt(record, 204));
        Assert.Equal(NetSegmentsSection.StartRightSmoothBit | NetSegmentsSection.EndLeftSmoothBit, record[208]);
        Assert.Equal(0, record[209] | record[210] | record[211]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsLanes(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new NetLanesSectionContent();
        content.Lanes.Add(Lane(70000, 2));
        content.Lanes.Add(Lane(5, 0));

        DumpSection section = WriteAndOpen(dump => NetLanesSectionWriter.Write(dump, content), NetLanesSection.Name);
        NetLanesSectionContent read = NetLanesSectionReader.Read(section);
        byte[] bytes = File.ReadAllBytes(section.GetFilePath(NetLanesSection.LanesFileName));

        Assert.Equal(2, section.RecordCount);
        Assert.Equal([5u, 70000u], read.Lanes.Select(lane => lane.Id));
        NetLaneRecord lane = read.Lanes[1];
        Assert.Equal((20u, (ushort)2, (ushort)0x34), (lane.Segment, lane.Index, lane.Flags));
        Assert.Equal(Lane(70000, 2).Bezier, lane.Bezier);
        Assert.Equal(30.5f, lane.Length);
        Assert.Equal(HeaderSize + (2 * 64), bytes.Length);
        byte[] record = bytes[(HeaderSize + 64)..];
        Assert.Equal(70000u, UInt32At(record, 0));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(8)));
        Assert.Equal(0x34, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(10)));
        Assert.Equal(-3f, SingleAt(record, 12));
        Assert.Equal(30.5f, SingleAt(record, 60));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsPrefabs(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new NetPrefabsSectionContent();
        content.Prefabs.Add(Prefab(47, "Basic Road"));
        NetPrefabRecord path = Prefab(3, "Pedestrian Path");
        path.ElevatedVariant = null;
        path.TunnelVariant = null;
        path.Lanes.Clear();
        content.Prefabs.Add(path);

        DumpSection section = WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, content), NetPrefabsSection.Name);
        NetPrefabsSectionContent read = NetPrefabsSectionReader.Read(section);

        Assert.Equal(2, section.RecordCount);
        Assert.Equal([3u, 47u], read.Prefabs.Select(prefab => prefab.Index));
        Assert.Null(read.Prefabs[0].ElevatedVariant);
        Assert.Null(read.Prefabs[0].TunnelVariant);
        Assert.Equal("Pedestrian Path Bridge", read.Prefabs[0].BridgeVariant);
        Assert.Empty(read.Prefabs[0].Lanes);
        NetPrefabRecord road = read.Prefabs[1];
        NetPrefabRecord expected = Prefab(47, "Basic Road");
        Assert.Equal(
            (expected.Name, expected.ClassName, expected.Service, expected.SubService, expected.Level, expected.Layer, expected.AiType),
            (road.Name, road.ClassName, road.Service, road.SubService, road.Level, road.Layer, road.AiType));
        Assert.Equal(
            (expected.Highway, expected.Bridge, expected.Tunnel, expected.Underground, expected.Overground),
            (road.Highway, road.Bridge, road.Tunnel, road.Underground, road.Overground));
        Assert.Equal(
            (expected.HalfWidth, expected.PavementWidth, expected.SegmentLength, expected.SurfaceLevel),
            (road.HalfWidth, road.PavementWidth, road.SegmentLength, road.SurfaceLevel));
        Assert.Equal(
            (expected.CreatePavement, expected.CreateGravel, expected.CreateRuining, expected.FlattenTerrain, expected.LowerTerrain, expected.ClipTerrain, expected.FollowTerrain, expected.FlatJunctions),
            (road.CreatePavement, road.CreateGravel, road.CreateRuining, road.FlattenTerrain, road.LowerTerrain, road.ClipTerrain, road.FollowTerrain, road.FlatJunctions));
        Assert.Equal(
            (expected.ElevatedVariant, expected.BridgeVariant, expected.SlopeVariant, expected.TunnelVariant),
            (road.ElevatedVariant, road.BridgeVariant, road.SlopeVariant, road.TunnelVariant));
        NetPrefabLane lane = Assert.Single(road.Lanes);
        Assert.Equal((-1.5f, 3f, 0.25f, 0.5f, 0.8f), (lane.Position, lane.Width, lane.VerticalOffset, lane.StopOffset, lane.SpeedLimit));
        Assert.Equal(((byte)1, (byte)2, (byte)1, 0x10001u, 0x1u), (lane.Direction, lane.FinalDirection, lane.LaneType, lane.VehicleType, lane.StopType));
    }

    [Fact]
    public void WritesTheDocumentedPrefabJson()
    {
        using var scope = new CultureScope("lv-LV");
        var content = new NetPrefabsSectionContent();
        content.Prefabs.Add(Prefab(47, "Basic Road"));

        DumpSection section = WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, content), NetPrefabsSection.Name);

        Assert.Equal([NetPrefabsSection.PrefabsFileName], section.Files.Select(file => file.Path));
        Assert.Equal(
            """
            {
              "prefabs": [
                {
                  "index": 47,
                  "name": "Basic Road",
                  "class": {
                    "name": "Small Road",
                    "service": "Road",
                    "subService": "None",
                    "level": "Level1",
                    "layer": 1
                  },
                  "ai": {
                    "type": "RoadAI",
                    "highway": false,
                    "bridge": false,
                    "tunnel": false,
                    "underground": false,
                    "overground": false
                  },
                  "halfWidth": 8,
                  "pavementWidth": 3,
                  "segmentLength": 64,
                  "surfaceLevel": -0.3,
                  "terrain": {
                    "createPavement": true,
                    "createGravel": false,
                    "createRuining": false,
                    "flattenTerrain": true,
                    "lowerTerrain": false,
                    "clipTerrain": true,
                    "followTerrain": false,
                    "flatJunctions": false
                  },
                  "variants": {
                    "elevated": "Basic Road Elevated",
                    "bridge": "Basic Road Bridge",
                    "slope": "Basic Road Slope",
                    "tunnel": "Basic Road Tunnel"
                  },
                  "lanes": [
                    {
                      "position": -1.5,
                      "width": 3,
                      "verticalOffset": 0.25,
                      "stopOffset": 0.5,
                      "speedLimit": 0.8,
                      "direction": 1,
                      "finalDirection": 2,
                      "laneType": 1,
                      "vehicleType": 65537,
                      "stopType": 1
                    }
                  ]
                }
              ]
            }

            """,
            File.ReadAllText(section.GetFilePath(NetPrefabsSection.PrefabsFileName)));
    }

    [Fact]
    public void RejectsDuplicatesAndPrefabsThatCantBeWritten()
    {
        var nodes = new NetNodesSectionContent();
        nodes.Nodes.Add(Node(4, 0));
        nodes.Nodes.Add(Node(4, 1));
        Assert.Contains("Node 4 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => NetNodesSectionWriter.Write(dump, nodes), NetNodesSection.Name)).Message, StringComparison.Ordinal);

        var segments = new NetSegmentsSectionContent();
        segments.Segments.Add(Segment(8));
        segments.Segments.Add(Segment(8));
        Assert.Contains("Segment 8 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => NetSegmentsSectionWriter.Write(dump, segments), NetSegmentsSection.Name)).Message, StringComparison.Ordinal);

        var lanes = new NetLanesSectionContent();
        lanes.Lanes.Add(Lane(2, 0));
        lanes.Lanes.Add(Lane(2, 1));
        Assert.Contains("Lane 2 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => NetLanesSectionWriter.Write(dump, lanes), NetLanesSection.Name)).Message, StringComparison.Ordinal);

        var prefabs = new NetPrefabsSectionContent();
        prefabs.Prefabs.Add(Prefab(1, "A"));
        prefabs.Prefabs.Add(Prefab(1, "B"));
        Assert.Contains("Prefab 1 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, prefabs), NetPrefabsSection.Name)).Message, StringComparison.Ordinal);

        NetPrefabRecord noWidth = Prefab(2, "No Width");
        noWidth.HalfWidth = float.NaN;
        Assert.Contains("half width", NetPrefabsSectionWriter.Check(noWidth), StringComparison.Ordinal);
        NetPrefabRecord hole = Prefab(2, "Hole");
        hole.Lanes.Add(null!);
        Assert.Equal("lane 1 is missing", NetPrefabsSectionWriter.Check(hole));
        NetPrefabRecord fastLane = Prefab(2, "Fast Lane");
        fastLane.Lanes[0].SpeedLimit = float.PositiveInfinity;
        Assert.Equal("a value of lane 0 isn't a finite number", NetPrefabsSectionWriter.Check(fastLane));
        Assert.Null(NetPrefabsSectionWriter.Check(Prefab(2, "Fine")));
        var invalid = new NetPrefabsSectionContent();
        invalid.Prefabs.Add(noWidth);
        Assert.Contains("Prefab 2 can't be written", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, invalid), NetPrefabsSection.Name)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAPrefabFileThatListsAnIndexTwice()
    {
        // The reader doesn't check the file's size again, so the opened section can be given other JSON.
        DumpSection section = WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, new NetPrefabsSectionContent()), NetPrefabsSection.Name);
        File.WriteAllText(section.GetFilePath(NetPrefabsSection.PrefabsFileName), "{ \"prefabs\": [ " + PrefabJson() + ", " + PrefabJson() + " ] }");

        DumpException error = Assert.Throws<DumpException>(() => NetPrefabsSectionReader.Read(section));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("$.prefabs[1]: prefab index 47 is listed twice", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAPrefabWithoutARequiredProperty()
    {
        var content = new NetPrefabsSectionContent();
        DumpSection section = WriteAndOpen(dump => NetPrefabsSectionWriter.Write(dump, content), NetPrefabsSection.Name);
        File.WriteAllText(section.GetFilePath(NetPrefabsSection.PrefabsFileName), "{ \"prefabs\": [ " + PrefabJson().Replace("\"halfWidth\": 8,", string.Empty, StringComparison.Ordinal) + " ] }");

        DumpException error = Assert.Throws<DumpException>(() => NetPrefabsSectionReader.Read(section));

        Assert.Contains("$.prefabs[0].halfWidth: this required property is missing", error.Message, StringComparison.Ordinal);
    }

    private static NetNodeRecord Node(uint id, float offset)
    {
        var node = new NetNodeRecord
        {
            Id = id,
            Flags = 0x80800081,
            Flags2 = 0x24,
            Prefab = 47,
            Position = new DumpVector3(-1200.5f + offset - 0.5f, 60.5f, 812.25f),
            Building = 77,
            Elevation = 12,
            HeightOffset = 3,
        };
        node.Segments[0] = 11;
        node.Segments[3] = 40000;
        node.Segments[7] = 65535;
        return node;
    }

    private static NetSegmentRecord Segment(uint id) => new()
    {
        Id = id,
        Flags = 0x10,
        Flags2 = 0x400,
        Prefab = 12,
        StartNode = 5,
        EndNode = 6,
        StartDirection = new DumpVector3(1, 0, 0),
        EndDirection = new DumpVector3(-1, 0.125f, -1),
        MiddlePosition = new DumpVector3(50, 60.25f, 0.5f),
        Bezier = new DumpBezier3(new DumpVector3(0, 60, 0), new DumpVector3(33.25f, 60, 0), new DumpVector3(66.5f, 60.5f, 0), new DumpVector3(100, 61, 0)),
        StartLeft = new NetCorner(new DumpVector3(0.25f, 60, -8), new DumpVector3(1, 0, 0), false),
        StartRight = new NetCorner(new DumpVector3(0.25f, 60, 8), new DumpVector3(1, 0, 0.001f), true),
        EndLeft = new NetCorner(new DumpVector3(99.75f, 61, 8), new DumpVector3(-1, 0, 0), true),
        EndRight = new NetCorner(new DumpVector3(99.75f, 61, -8), new DumpVector3(-1, 0, 0), false),
        AverageLength = 99.5f,
    };

    private static NetLaneRecord Lane(uint id, ushort index) => new()
    {
        Id = id,
        Segment = 20,
        Index = index,
        Flags = 0x34,
        Bezier = new DumpBezier3(new DumpVector3(-3, 60, 1), new DumpVector3(7, 60, 1), new DumpVector3(17, 60.5f, 1), new DumpVector3(27.5f, 61, 1)),
        Length = 30.5f,
    };

    private static NetPrefabRecord Prefab(uint index, string name)
    {
        var prefab = new NetPrefabRecord
        {
            Index = index,
            Name = name,
            ClassName = "Small Road",
            Service = "Road",
            SubService = "None",
            Level = "Level1",
            Layer = 1,
            AiType = "RoadAI",
            HalfWidth = 8,
            PavementWidth = 3,
            SegmentLength = 64,
            SurfaceLevel = -0.3f,
            CreatePavement = true,
            FlattenTerrain = true,
            ClipTerrain = true,
            ElevatedVariant = name + " Elevated",
            BridgeVariant = name + " Bridge",
            SlopeVariant = name + " Slope",
            TunnelVariant = name + " Tunnel",
        };
        prefab.Lanes.Add(new NetPrefabLane
        {
            Position = -1.5f,
            Width = 3,
            VerticalOffset = 0.25f,
            StopOffset = 0.5f,
            SpeedLimit = 0.8f,
            Direction = 1,
            FinalDirection = 2,
            LaneType = 1,
            VehicleType = 0x10001,
            StopType = 1,
        });
        return prefab;
    }

    private static string PrefabJson() =>
        """
        {
          "index": 47, "name": "Basic Road",
          "class": { "name": "Small Road", "service": "Road", "subService": "None", "level": "Level1", "layer": 1 },
          "ai": { "type": "RoadAI", "highway": false, "bridge": false, "tunnel": false, "underground": false, "overground": false },
          "halfWidth": 8, "pavementWidth": 3, "segmentLength": 64, "surfaceLevel": -0.3,
          "terrain": { "createPavement": true, "createGravel": false, "createRuining": false, "flattenTerrain": true, "lowerTerrain": false, "clipTerrain": true, "followTerrain": false, "flatJunctions": false },
          "variants": {},
          "lanes": []
        }
        """;

    private static byte[] OnlyRecord(DumpSection section, string path, int size)
    {
        byte[] bytes = File.ReadAllBytes(section.GetFilePath(path));
        Assert.Equal(HeaderSize + size, bytes.Length);
        return bytes[HeaderSize..];
    }

    private static uint UInt32At(byte[] record, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));

    private static float SingleAt(byte[] record, int offset) => BinaryPrimitives.ReadSingleLittleEndian(record.AsSpan(offset));

    private DumpSection WriteAndOpen(Action<DumpWriter> write, string sectionName)
    {
        DumpWriter dump = DumpWriter.Create(_temp.PathOf("dump" + (++_dumps).ToString(System.Globalization.CultureInfo.InvariantCulture)), TestManifests.Minimal());
        write(dump);
        dump.Manifest.Complete = true;
        dump.WriteManifest();
        return DumpReader.Open(dump.Folder).FindSection(sectionName)!;
    }
}
