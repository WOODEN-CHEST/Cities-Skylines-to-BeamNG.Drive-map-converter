using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Scene;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class SceneTreeWriterTests
{
    private const string Level = "test_level";

    [Fact]
    public void WritesOneFolderPerGroupAndOneLinePerObject()
    {
        IReadOnlyList<OutputFile> files = SceneTreeWriter.CreateFiles(Level, CreateTree());

        Assert.Equal(
            [
                "/levels/test_level/main/items.level.json",
                "/levels/test_level/main/MissionGroup/items.level.json",
                "/levels/test_level/main/MissionGroup/objects/items.level.json",
                "/levels/test_level/main/MissionGroup/PlayerDropPoints/items.level.json",
            ],
            files.Select(file => file.Path.Value));

        // The persistent ids are Python's uuid.uuid5 of "test_level:<key>" in the tool's namespace.
        Assert.Equal(
            """
            {"name":"MissionGroup","class":"SimGroup","persistentId":"974bcf9b-3404-51c8-82eb-9f6cebab9597"}

            """,
            files[0].Text());
        Assert.Equal(
            """
            {"name":"objects","class":"SimGroup","persistentId":"37904964-a221-5bc8-ba7f-a68641246111","__parent":"MissionGroup"}
            {"name":"PlayerDropPoints","class":"SimGroup","persistentId":"0ecbe08a-f72a-5965-85ad-3e8985d97347","__parent":"MissionGroup"}

            """,
            files[1].Text());
        Assert.Equal(
            """
            {"class":"TSStatic","persistentId":"3aba5b9f-cb36-5f97-b2ca-8861f92576b2","__parent":"objects","position":[1.5,-2.25,3],"collisionType":"Collision Mesh","rotationMatrix":[1,0,0,0,1,0,0,0,1],"scale":[1,1,1],"shapeName":"/levels/test_level/art/box.dae"}
            {"class":"DecalRoad","persistentId":"bbfdcd56-c29c-5105-9a0f-f3b7ce14a568","__parent":"objects","position":[0,0,10],"autoLanes":false,"drivability":1,"improvedSpline":true,"lanesLeft":1,"lanesRight":1,"material":"road_invisible","nodes":[[0,0,10,7],[100,0.5,10.25,7]]}

            """,
            files[2].Text());
        Assert.Equal(
            """
            {"name":"spawn_a","class":"SpawnSphere","persistentId":"bfad763b-a311-528f-ab66-0ea238fc8851","__parent":"PlayerDropPoints","position":[0,0,0.5],"dataBlock":"SpawnSphereMarker","radius":1,"rotationMatrix":[0,1,0,-1,0,0,0,0,1]}

            """,
            files[3].Text());
    }

    [Fact]
    public void WritesTheSameBytesUnderADecimalCommaCulture()
    {
        Dictionary<string, byte[]> expected = OutputFiles.Snapshot(SceneTreeWriter.CreateFiles(Level, CreateTree()));
        using (new CultureScope("lv-LV"))
        {
            Assert.Equal(expected, OutputFiles.Snapshot(SceneTreeWriter.CreateFiles(Level, CreateTree())));
        }
    }

    [Fact]
    public void WritesValidJsonObjectsOneToALine()
    {
        foreach (OutputFile file in SceneTreeWriter.CreateFiles(Level, CreateTree()))
        {
            string[] lines = file.Text().Split('\n');
            Assert.Equal(string.Empty, lines[^1]);
            foreach (string line in lines[..^1])
            {
                Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(line).RootElement.ValueKind);
            }
        }
    }

    [Fact]
    public void WritesGenericObjectsWithTheirFieldsInOrdinalOrder()
    {
        var root = new SimGroup(SceneTreeWriter.RootGroupName);
        var sky = new GenericSceneObject("ScatterSky") { Key = "sky", Name = "sunsky", Position = Vector3.Zero };
        sky.Fields.Set("skyBrightness", 40f);
        sky.Fields.Set("Textures", new JsonArray(new JsonObject { ["texSpeed"] = 0.002f }));
        sky.Fields.Set("ambientScale", new Vector3(1, 0.5f, 0.25f));
        sky.Fields.Set("useNightCubemap", true);
        sky.Fields.Set("count", 3);
        sky.Fields.Set("flareType", "BNG_Sunflare_3");
        root.Children.Add(sky);

        string line = SceneTreeWriter.CreateFiles(Level, root)[1].Text();

        Assert.Equal(
            "{\"name\":\"sunsky\",\"class\":\"ScatterSky\",\"persistentId\":\"" + PersistentId.ForObject(Level, "sky")
            + "\",\"__parent\":\"MissionGroup\",\"position\":[0,0,0],\"Textures\":[{\"texSpeed\":0.002}],\"ambientScale\":[1,0.5,0.25],"
            + "\"count\":3,\"flareType\":\"BNG_Sunflare_3\",\"skyBrightness\":40,\"useNightCubemap\":true}\n",
            line);
    }

    [Fact]
    public void WritesAnEmptyFileForAnEmptyGroup()
    {
        var root = new SimGroup(SceneTreeWriter.RootGroupName);
        root.Children.Add(new SimGroup("empty"));

        IReadOnlyList<OutputFile> files = SceneTreeWriter.CreateFiles(Level, root);

        Assert.Empty(files.Single("/levels/test_level/main/MissionGroup/empty/items.level.json").ToBytes());
    }

    [Fact]
    public void RequiresTheRootToBeTheMissionGroup() =>
        Assert.Throws<ArgumentException>(() => SceneTreeWriter.CreateFiles(Level, new SimGroup("Root")));

    [Fact]
    public void RejectsNamesThatDifferOnlyInCase()
    {
        SimGroup root = CreateTree();
        root.Children.Add(new SpawnSphere { Key = "other", Name = "SPAWN_A", Position = Vector3.Zero });

        Assert.Throws<ArgumentException>(() => SceneTreeWriter.CreateFiles(Level, root));
    }

    [Fact]
    public void RejectsRepeatedKeys()
    {
        SimGroup root = CreateTree();
        root.Children.Add(new TSStatic { Key = "box", ShapeName = VirtualPath.Parse("/x.dae"), Position = Vector3.Zero });

        Assert.Throws<ArgumentException>(() => SceneTreeWriter.CreateFiles(Level, root));
    }

    [Fact]
    public void RejectsAnObjectInTwoPlaces()
    {
        SimGroup root = CreateTree();
        var group = new SimGroup("again");
        group.Children.Add(root.Children[0]);
        root.Children.Add(group);

        Assert.Throws<ArgumentException>(() => SceneTreeWriter.CreateFiles(Level, root));
    }

    [Fact]
    public void IgnoresChangesToTheTreeAfterCreatingTheFiles()
    {
        SimGroup root = CreateTree();
        IReadOnlyList<OutputFile> files = SceneTreeWriter.CreateFiles(Level, root);
        string before = files[1].Text();

        root.Children.Clear();

        Assert.Equal(before, files[1].Text());
    }

    [Theory]
    [InlineData("name")]
    [InlineData("class")]
    [InlineData("persistentId")]
    [InlineData("__parent")]
    [InlineData("position")]
    public void ReservesTheFieldsTheWriterSets(string name) =>
        Assert.Throws<ArgumentException>(() => new SceneFields().Set(name, "x"));

    [Fact]
    public void RejectsInvalidNamesAndRoads()
    {
        Assert.Throws<ArgumentException>(() => new SimGroup("has space"));
        Assert.Throws<ArgumentException>(() => new GenericSceneObject("Bad-Class") { Key = "k" });
        Assert.Throws<ArgumentException>(() => new DecalRoad
        {
            Key = "road",
            Material = DecalRoad.InvisibleMaterial,
            Nodes = [new DecalRoadNode(Vector3.Zero, 7)],
        });
        Assert.Throws<ArgumentException>(() => new DecalRoad
        {
            Key = "road",
            Material = DecalRoad.InvisibleMaterial,
            Nodes = [new DecalRoadNode(Vector3.Zero, 7), new DecalRoadNode(Vector3.UnitX, 0)],
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DecalRoad
        {
            Key = "road",
            Material = DecalRoad.InvisibleMaterial,
            Nodes = [new DecalRoadNode(Vector3.Zero, 7), new DecalRoadNode(Vector3.UnitX, 7)],
            LanesLeft = -1,
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => BeamNGJson.ToJson(float.NaN));
    }

    private static SimGroup CreateTree()
    {
        var objects = new SimGroup("objects");
        objects.Children.Add(new TSStatic
        {
            Key = "box",
            ShapeName = VirtualPath.Parse("/levels/test_level/art/box.dae"),
            Position = new Vector3(1.5f, -2.25f, 3),
        });
        objects.Children.Add(new DecalRoad
        {
            Key = "road",
            Nodes = [new DecalRoadNode(new Vector3(0, 0, 10), 7), new DecalRoadNode(new Vector3(100, 0.5f, 10.25f), 7)],
            Material = DecalRoad.InvisibleMaterial,
            Drivability = 1,
            LanesLeft = 1,
            LanesRight = 1,
            ImprovedSpline = true,
        });

        var spawnPoints = new SimGroup("PlayerDropPoints");
        spawnPoints.Children.Add(new SpawnSphere
        {
            Key = "spawn",
            Name = "spawn_a",
            Position = new Vector3(0, 0, 0.5f),
            Rotation = SpawnSphere.RotationForHeading(new Vector2(1, 0)),
        });

        var root = new SimGroup(SceneTreeWriter.RootGroupName);
        root.Children.Add(objects);
        root.Children.Add(spawnPoints);
        return root;
    }
}
