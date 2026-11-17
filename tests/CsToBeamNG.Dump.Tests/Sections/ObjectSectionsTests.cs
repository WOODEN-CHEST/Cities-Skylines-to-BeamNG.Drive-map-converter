using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Dump.Tests.Sections;

public sealed class ObjectSectionsTests : IDisposable
{
    private const int HeaderSize = 64;

    private readonly TempFolder _temp = new();
    private int _dumps;

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsBuildings(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new BuildingsSectionContent();
        content.Buildings.Add(Building(40000));
        content.Buildings.Add(Building(7));

        DumpSection section = WriteAndOpen(dump => BuildingsSectionWriter.Write(dump, content), BuildingsSection.Name);
        BuildingsSectionContent read = BuildingsSectionReader.Read(section);

        Assert.Equal(BuildingsSection.Version, section.Version);
        Assert.Equal(2, section.RecordCount);
        Assert.Equal([7u, 40000u], read.Buildings.Select(building => building.Id));
        BuildingRecord building = read.Buildings[1];
        BuildingRecord expected = Building(40000);
        Assert.Equal((expected.Flags, expected.Flags2, expected.Prefab), (building.Flags, building.Flags2, building.Prefab));
        Assert.Equal(expected.Position, building.Position);
        Assert.Equal(expected.Angle, building.Angle);
        Assert.Equal(expected.MeshPosition, building.MeshPosition);
        Assert.Equal((expected.ParentBuilding, expected.SubBuilding), (building.ParentBuilding, building.SubBuilding));
        Assert.Equal(expected.Color, building.Color);
        Assert.Equal((expected.Width, expected.Length, expected.Level, expected.BaseHeight), (building.Width, building.Length, building.Level, building.BaseHeight));
        Assert.Equal(HeaderSize + (2 * 72), new FileInfo(section.GetFilePath(BuildingsSection.BuildingsFileName)).Length);
    }

    [Fact]
    public void WritesBuildingsWithTheDocumentedLayout()
    {
        var content = new BuildingsSectionContent();
        content.Buildings.Add(Building(40000));

        byte[] record = OnlyRecord(WriteAndOpen(dump => BuildingsSectionWriter.Write(dump, content), BuildingsSection.Name), BuildingsSection.BuildingsFileName, BuildingsSection.BuildingRecordSize);

        Assert.Equal(40000u, UInt32At(record, 0));
        Assert.Equal(0x8010001u, UInt32At(record, 4));
        Assert.Equal(0x4u, UInt32At(record, 8));
        Assert.Equal(312u, UInt32At(record, 12));
        Assert.Equal((-1200.5f, 61.25f, 812.75f), (SingleAt(record, 16), SingleAt(record, 20), SingleAt(record, 24)));
        Assert.Equal(1.5f, SingleAt(record, 28));
        Assert.Equal((-1196.5f, 61.25f, 810.25f), (SingleAt(record, 32), SingleAt(record, 36), SingleAt(record, 40)));
        Assert.Equal((12u, 13u), (UInt32At(record, 44), UInt32At(record, 48)));
        Assert.Equal((0.25f, 0.5f, 0.75f, 1f), (SingleAt(record, 52), SingleAt(record, 56), SingleAt(record, 60), SingleAt(record, 64)));
        Assert.Equal(new byte[] { 3, 4, 2, 9 }, record[68..72]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsProps(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new PropsSectionContent();
        content.Props.Add(Prop(65535));
        content.Props.Add(Prop(2));

        DumpSection section = WriteAndOpen(dump => PropsSectionWriter.Write(dump, content), PropsSection.Name);
        PropsSectionContent read = PropsSectionReader.Read(section);
        byte[] bytes = File.ReadAllBytes(section.GetFilePath(PropsSection.PropsFileName));

        Assert.Equal(2, section.RecordCount);
        Assert.Equal([2u, 65535u], read.Props.Select(prop => prop.Id));
        PropRecord prop = read.Props[1];
        PropRecord expected = Prop(65535);
        Assert.Equal((expected.Prefab, expected.Position, expected.Angle, expected.Scale), (prop.Prefab, prop.Position, prop.Angle, prop.Scale));
        Assert.Equal((expected.Color, expected.Flags), (prop.Color, prop.Flags));
        Assert.Equal(HeaderSize + (2 * 48), bytes.Length);
        byte[] record = bytes[(HeaderSize + 48)..];
        Assert.Equal((65535u, 77u), (UInt32At(record, 0), UInt32At(record, 4)));
        Assert.Equal((300.25f, 60.5f, -42.75f), (SingleAt(record, 8), SingleAt(record, 12), SingleAt(record, 16)));
        Assert.Equal((0.75f, 1.25f), (SingleAt(record, 20), SingleAt(record, 24)));
        Assert.Equal((1f, 0.5f, 0.25f, 0f), (SingleAt(record, 28), SingleAt(record, 32), SingleAt(record, 36), SingleAt(record, 40)));
        Assert.Equal(0x21, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)));
        Assert.Equal(0, record[46] | record[47]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsTrees(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new TreesSectionContent();
        content.Trees.Add(Tree(262143));
        content.Trees.Add(Tree(1));

        DumpSection section = WriteAndOpen(dump => TreesSectionWriter.Write(dump, content), TreesSection.Name);
        TreesSectionContent read = TreesSectionReader.Read(section);
        byte[] bytes = File.ReadAllBytes(section.GetFilePath(TreesSection.TreesFileName));

        Assert.Equal(2, section.RecordCount);
        Assert.Equal([1u, 262143u], read.Trees.Select(tree => tree.Id));
        TreeRecord tree = read.Trees[1];
        TreeRecord expected = Tree(262143);
        Assert.Equal((expected.Prefab, expected.Position, expected.Scale, expected.Brightness, expected.Flags), (tree.Prefab, tree.Position, tree.Scale, tree.Brightness, tree.Flags));
        Assert.Equal(15, tree.GrowState);
        Assert.Equal(HeaderSize + (2 * 32), bytes.Length);
        byte[] record = bytes[(HeaderSize + 32)..];
        Assert.Equal((262143u, 5u), (UInt32At(record, 0), UInt32At(record, 4)));
        Assert.Equal((-8000.5f, 120.25f, 7999.75f), (SingleAt(record, 8), SingleAt(record, 12), SingleAt(record, 16)));
        Assert.Equal((1.125f, 0.875f), (SingleAt(record, 20), SingleAt(record, 24)));
        Assert.Equal(0xF21, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(28)));
        Assert.Equal(0, record[30] | record[31]);
    }

    [Fact]
    public void TellsWhichObjectsTheGameDraws()
    {
        Assert.True(new BuildingRecord { Flags = BuildingRecord.CreatedFlag | BuildingRecord.CompletedFlag }.IsDrawn);
        Assert.False(new BuildingRecord { Flags = BuildingRecord.CreatedFlag | BuildingRecord.HiddenFlag }.IsDrawn);
        Assert.False(new BuildingRecord { Flags = BuildingRecord.CreatedFlag | BuildingRecord.DeletedFlag }.IsDrawn);
        Assert.True(new PropRecord { Flags = PropRecord.CreatedFlag | PropRecord.FixedHeightFlag }.IsDrawn);
        Assert.False(new PropRecord { Flags = PropRecord.CreatedFlag | PropRecord.BlockedFlag }.IsDrawn);
        Assert.False(new PropRecord { Flags = PropRecord.CreatedFlag | PropRecord.HiddenFlag }.IsDrawn);
        Assert.True(new TreeRecord { Flags = TreeRecord.CreatedFlag | 0x100 }.IsDrawn);
        Assert.False(new TreeRecord { Flags = TreeRecord.CreatedFlag }.IsDrawn);
        Assert.False(new TreeRecord { Flags = TreeRecord.CreatedFlag | TreeRecord.HiddenFlag | 0x100 }.IsDrawn);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsBuildingPrefabs(string culture)
    {
        using var scope = new CultureScope(culture);
        var content = new BuildingPrefabsSectionContent();
        content.Prefabs.Add(BuildingPrefab(312, "L1 2x2 Detached01"));
        BuildingPrefabRecord bare = BuildingPrefab(5, "Bare");
        bare.Mesh = null;
        bare.LodMesh = null;
        bare.SubBuildings.Clear();
        content.Prefabs.Add(bare);

        DumpSection section = WriteAndOpen(dump => BuildingPrefabsSectionWriter.Write(dump, content), BuildingPrefabsSection.Name);
        BuildingPrefabsSectionContent read = BuildingPrefabsSectionReader.Read(section);

        Assert.Equal(2, section.RecordCount);
        Assert.Equal([5u, 312u], read.Prefabs.Select(prefab => prefab.Index));
        Assert.Null(read.Prefabs[0].Mesh);
        Assert.Null(read.Prefabs[0].LodMesh);
        Assert.Empty(read.Prefabs[0].SubBuildings);
        BuildingPrefabRecord prefab = read.Prefabs[1];
        BuildingPrefabRecord expected = BuildingPrefab(312, "L1 2x2 Detached01");
        Assert.Equal((expected.Name, expected.AiType, expected.CellWidth, expected.CellLength), (prefab.Name, prefab.AiType, prefab.CellWidth, prefab.CellLength));
        Assert.Equal(
            (expected.Class.Name, expected.Class.Service, expected.Class.SubService, expected.Class.Level, expected.Class.Layer),
            (prefab.Class.Name, prefab.Class.Service, prefab.Class.SubService, prefab.Class.Level, prefab.Class.Layer));
        Assert.Equal((expected.Size, expected.Mesh, expected.LodMesh), (prefab.Size, prefab.Mesh, prefab.LodMesh));
        Assert.Equal((expected.PlacementMode, expected.ZoningMode, expected.Circular, expected.ExpandFrontYard), (prefab.PlacementMode, prefab.ZoningMode, prefab.Circular, prefab.ExpandFrontYard));
        Assert.Equal(
            (expected.FlattenTerrain, expected.FlattenFullArea, expected.FullPavement, expected.FullGravel, expected.ClipTerrain, expected.RequireHeightMap, expected.UseColorVariations),
            (prefab.FlattenTerrain, prefab.FlattenFullArea, prefab.FullPavement, prefab.FullGravel, prefab.ClipTerrain, prefab.RequireHeightMap, prefab.UseColorVariations));
        Assert.Equal(expected.Colors, prefab.Colors);
        Assert.Equal(2, prefab.SubBuildings.Count);
        Assert.Equal((313u, "Sub", new DumpVector3(4, 0, -8), 90f, true), (prefab.SubBuildings[0].Prefab, prefab.SubBuildings[0].Name, prefab.SubBuildings[0].Position, prefab.SubBuildings[0].AngleDegrees, prefab.SubBuildings[0].FixedHeight));
        Assert.Equal((null, string.Empty), (prefab.SubBuildings[1].Prefab, prefab.SubBuildings[1].Name));
    }

    [Fact]
    public void WritesTheDocumentedBuildingPrefabJson()
    {
        using var scope = new CultureScope("lv-LV");
        var content = new BuildingPrefabsSectionContent();
        BuildingPrefabRecord prefab = BuildingPrefab(312, "L1 2x2 Detached01");
        prefab.SubBuildings.RemoveAt(1);
        content.Prefabs.Add(prefab);

        DumpSection section = WriteAndOpen(dump => BuildingPrefabsSectionWriter.Write(dump, content), BuildingPrefabsSection.Name);

        Assert.Equal([BuildingPrefabsSection.PrefabsFileName], section.Files.Select(file => file.Path));
        Assert.Equal(
            """
            {
              "prefabs": [
                {
                  "index": 312,
                  "name": "L1 2x2 Detached01",
                  "class": {
                    "name": "Low Residential - Level1",
                    "service": "Residential",
                    "subService": "ResidentialLow",
                    "level": "Level1",
                    "layer": 1
                  },
                  "ai": {
                    "type": "ResidentialBuildingAI"
                  },
                  "cellWidth": 2,
                  "cellLength": 3,
                  "size": {
                    "x": 16,
                    "y": 9.5,
                    "z": 24
                  },
                  "mesh": {
                    "min": {
                      "x": -8,
                      "y": -0.5,
                      "z": -12
                    },
                    "max": {
                      "x": 8,
                      "y": 9.5,
                      "z": 12
                    }
                  },
                  "lodMesh": {
                    "min": {
                      "x": -7.75,
                      "y": 0,
                      "z": -11.5
                    },
                    "max": {
                      "x": 7.75,
                      "y": 9.25,
                      "z": 11.5
                    }
                  },
                  "placementMode": "Roadside",
                  "zoningMode": "Straight",
                  "circular": false,
                  "expandFrontYard": true,
                  "terrain": {
                    "flattenTerrain": true,
                    "flattenFullArea": false,
                    "fullPavement": false,
                    "fullGravel": true,
                    "clipTerrain": false
                  },
                  "requireHeightMap": false,
                  "useColorVariations": true,
                  "colors": [
                    {
                      "r": 1,
                      "g": 0.5,
                      "b": 0.25,
                      "a": 0
                    },
                    {
                      "r": 0.5,
                      "g": 0.5,
                      "b": 0.25,
                      "a": 0
                    },
                    {
                      "r": 0.25,
                      "g": 0.5,
                      "b": 0.25,
                      "a": 0
                    },
                    {
                      "r": 0.125,
                      "g": 0.5,
                      "b": 0.25,
                      "a": 0
                    }
                  ],
                  "subBuildings": [
                    {
                      "prefab": 313,
                      "name": "Sub",
                      "position": {
                        "x": 4,
                        "y": 0,
                        "z": -8
                      },
                      "angle": 90,
                      "fixedHeight": true
                    }
                  ]
                }
              ]
            }

            """,
            File.ReadAllText(section.GetFilePath(BuildingPrefabsSection.PrefabsFileName)));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    public void RoundTripsPropAndTreePrefabs(string culture)
    {
        using var scope = new CultureScope(culture);
        var props = new PropPrefabsSectionContent();
        props.Prefabs.Add(PropPrefab(77, "Bench 01"));
        PropPrefabRecord decal = PropPrefab(3, "Decal");
        decal.LodMesh = null;
        decal.Decal = true;
        props.Prefabs.Add(decal);
        var trees = new TreePrefabsSectionContent();
        trees.Prefabs.Add(TreePrefab(5, "Tree Oak"));

        PropPrefabsSectionContent readProps = PropPrefabsSectionReader.Read(WriteAndOpen(dump => PropPrefabsSectionWriter.Write(dump, props), PropPrefabsSection.Name));
        TreePrefabsSectionContent readTrees = TreePrefabsSectionReader.Read(WriteAndOpen(dump => TreePrefabsSectionWriter.Write(dump, trees), TreePrefabsSection.Name));

        Assert.Equal([3u, 77u], readProps.Prefabs.Select(prefab => prefab.Index));
        Assert.True(readProps.Prefabs[0].Decal);
        Assert.Null(readProps.Prefabs[0].LodMesh);
        PropPrefabRecord prop = readProps.Prefabs[1];
        PropPrefabRecord expectedProp = PropPrefab(77, "Bench 01");
        Assert.Equal((expectedProp.Name, expectedProp.Class.Service, expectedProp.Mesh, expectedProp.LodMesh), (prop.Name, prop.Class.Service, prop.Mesh, prop.LodMesh));
        Assert.Equal((expectedProp.MinScale, expectedProp.MaxScale), (prop.MinScale, prop.MaxScale));
        Assert.Equal(
            (expectedProp.Decal, expectedProp.Marker, expectedProp.RequireHeightMap, expectedProp.RequireWaterMap, expectedProp.CreateRuining, expectedProp.LightEffects, expectedProp.UseColorVariations),
            (prop.Decal, prop.Marker, prop.RequireHeightMap, prop.RequireWaterMap, prop.CreateRuining, prop.LightEffects, prop.UseColorVariations));
        Assert.Equal(expectedProp.Colors, prop.Colors);
        TreePrefabRecord tree = Assert.Single(readTrees.Prefabs);
        TreePrefabRecord expectedTree = TreePrefab(5, "Tree Oak");
        Assert.Equal((expectedTree.Name, expectedTree.Class.Name, expectedTree.Mesh), (tree.Name, tree.Class.Name, tree.Mesh));
        Assert.Equal((expectedTree.MinScale, expectedTree.MaxScale, expectedTree.MinBrightness, expectedTree.MaxBrightness), (tree.MinScale, tree.MaxScale, tree.MinBrightness, tree.MaxBrightness));
        Assert.Equal((expectedTree.DefaultColor, expectedTree.CreateRuining), (tree.DefaultColor, tree.CreateRuining));
    }

    [Fact]
    public void WritesTheDocumentedPropAndTreePrefabJson()
    {
        using var scope = new CultureScope("lv-LV");
        var props = new PropPrefabsSectionContent();
        PropPrefabRecord bench = PropPrefab(77, "Bench 01");
        bench.Mesh = null;
        bench.LodMesh = null;
        props.Prefabs.Add(bench);
        var trees = new TreePrefabsSectionContent();
        trees.Prefabs.Add(TreePrefab(5, "Tree Oak"));

        DumpSection propSection = WriteAndOpen(dump => PropPrefabsSectionWriter.Write(dump, props), PropPrefabsSection.Name);
        DumpSection treeSection = WriteAndOpen(dump => TreePrefabsSectionWriter.Write(dump, trees), TreePrefabsSection.Name);

        Assert.Equal(
            """
            {
              "prefabs": [
                {
                  "index": 77,
                  "name": "Bench 01",
                  "class": {
                    "name": "Beautification Item",
                    "service": "Beautification",
                    "subService": "None",
                    "level": "Level1",
                    "layer": 1
                  },
                  "minScale": 0.75,
                  "maxScale": 1.25,
                  "decal": false,
                  "marker": false,
                  "requireHeightMap": false,
                  "requireWaterMap": true,
                  "createRuining": false,
                  "lightEffects": true,
                  "useColorVariations": false,
                  "colors": [
                    {
                      "r": 1,
                      "g": 1,
                      "b": 1,
                      "a": 1
                    },
                    {
                      "r": 1,
                      "g": 1,
                      "b": 1,
                      "a": 1
                    },
                    {
                      "r": 1,
                      "g": 1,
                      "b": 1,
                      "a": 1
                    },
                    {
                      "r": 1,
                      "g": 1,
                      "b": 1,
                      "a": 1
                    }
                  ]
                }
              ]
            }

            """,
            File.ReadAllText(propSection.GetFilePath(PropPrefabsSection.PrefabsFileName)));
        Assert.Equal(
            """
            {
              "prefabs": [
                {
                  "index": 5,
                  "name": "Tree Oak",
                  "class": {
                    "name": "Beautification Item",
                    "service": "Beautification",
                    "subService": "None",
                    "level": "Level1",
                    "layer": 1
                  },
                  "mesh": {
                    "min": {
                      "x": -3.5,
                      "y": 0,
                      "z": -3.25
                    },
                    "max": {
                      "x": 3.5,
                      "y": 12.75,
                      "z": 3.25
                    }
                  },
                  "minScale": 0.5,
                  "maxScale": 1.5,
                  "minBrightness": 0.75,
                  "maxBrightness": 1.25,
                  "defaultColor": {
                    "r": 0.25,
                    "g": 0.5,
                    "b": 0.125,
                    "a": 1
                  },
                  "createRuining": true
                }
              ]
            }

            """,
            File.ReadAllText(treeSection.GetFilePath(TreePrefabsSection.PrefabsFileName)));
    }

    [Fact]
    public void RejectsDuplicatesAndPrefabsThatCantBeWritten()
    {
        var buildings = new BuildingsSectionContent();
        buildings.Buildings.Add(Building(4));
        buildings.Buildings.Add(Building(4));
        Assert.Contains("Building 4 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => BuildingsSectionWriter.Write(dump, buildings), BuildingsSection.Name)).Message, StringComparison.Ordinal);
        var props = new PropsSectionContent();
        props.Props.Add(Prop(9));
        props.Props.Add(null!);
        Assert.Throws<ArgumentNullException>(() => WriteAndOpen(dump => PropsSectionWriter.Write(dump, props), PropsSection.Name));
        var trees = new TreesSectionContent();
        trees.Trees.Add(Tree(8));
        trees.Trees.Add(Tree(8));
        Assert.Contains("Tree 8 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => TreesSectionWriter.Write(dump, trees), TreesSection.Name)).Message, StringComparison.Ordinal);

        BuildingPrefabRecord noSize = BuildingPrefab(1, "No Size");
        noSize.Size = new DumpVector3(float.NaN, 0, 0);
        Assert.Equal("its size or the bounds of a mesh aren't finite numbers", BuildingPrefabsSectionWriter.Check(noSize));
        BuildingPrefabRecord hole = BuildingPrefab(1, "Hole");
        hole.SubBuildings.Add(null!);
        Assert.Equal("sub-building 2 or its name is missing", BuildingPrefabsSectionWriter.Check(hole));
        BuildingPrefabRecord huge = BuildingPrefab(1, "Huge");
        huge.CellLength = 256;
        Assert.Equal("its size of 2 × 256 cells is out of range", BuildingPrefabsSectionWriter.Check(huge));
        BuildingPrefabRecord unnamed = BuildingPrefab(1, "Unnamed");
        unnamed.Class = null!;
        Assert.Equal("its class or the class's name, service, sub-service or level is missing", BuildingPrefabsSectionWriter.Check(unnamed));
        Assert.Null(BuildingPrefabsSectionWriter.Check(BuildingPrefab(1, "Fine")));
        var invalid = new BuildingPrefabsSectionContent();
        invalid.Prefabs.Add(noSize);
        Assert.Contains("Prefab 1 can't be written", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => BuildingPrefabsSectionWriter.Write(dump, invalid), BuildingPrefabsSection.Name)).Message, StringComparison.Ordinal);

        PropPrefabRecord bench = PropPrefab(2, "Bench");
        bench.MaxScale = float.PositiveInfinity;
        Assert.Equal("its scales or the bounds of a mesh aren't finite numbers", PropPrefabsSectionWriter.Check(bench));
        PropPrefabRecord tinted = PropPrefab(2, "Tinted");
        tinted.Colors[3] = new DumpColor(float.NaN, 0, 0, 0);
        Assert.Equal("a colour isn't finite", PropPrefabsSectionWriter.Check(tinted));
        TreePrefabRecord tree = TreePrefab(3, "Tree");
        tree.MaxBrightness = float.NaN;
        Assert.Equal("its scales, brightness, default colour or mesh bounds aren't finite numbers", TreePrefabsSectionWriter.Check(tree));
        var duplicates = new TreePrefabsSectionContent();
        duplicates.Prefabs.Add(TreePrefab(3, "A"));
        duplicates.Prefabs.Add(TreePrefab(3, "B"));
        Assert.Contains("Prefab 3 is listed twice", Assert.Throws<ArgumentException>(() => WriteAndOpen(dump => TreePrefabsSectionWriter.Write(dump, duplicates), TreePrefabsSection.Name)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsPrefabsWithPropertiesThatALaterMinorVersionAdds()
    {
        // Later versions add mesh and material references as optional properties (docs/dump-format.md, Buildings, props
        // and trees); a 0.1 reader ignores them.
        DumpSection section = WriteAndOpen(dump => BuildingPrefabsSectionWriter.Write(dump, new BuildingPrefabsSectionContent()), BuildingPrefabsSection.Name);
        File.WriteAllText(
            section.GetFilePath(BuildingPrefabsSection.PrefabsFileName),
            "{ \"prefabs\": [ " + BuildingPrefabJson("\"mesh\": { \"min\": { \"x\": -1, \"y\": 0, \"z\": -1 }, \"max\": { \"x\": 1, \"y\": 2, \"z\": 1 }, \"file\": \"meshes/1.bin\", \"materials\": [ 4 ] },") + " ] }");

        BuildingPrefabRecord prefab = Assert.Single(BuildingPrefabsSectionReader.Read(section).Prefabs);

        Assert.Equal(new DumpBounds(new DumpVector3(-1, 0, -1), new DumpVector3(1, 2, 1)), prefab.Mesh);
        Assert.Null(prefab.LodMesh);
    }

    [Fact]
    public void RejectsPrefabFilesWithMissingOrWrongValues()
    {
        DumpSection section = WriteAndOpen(dump => BuildingPrefabsSectionWriter.Write(dump, new BuildingPrefabsSectionContent()), BuildingPrefabsSection.Name);
        string path = section.GetFilePath(BuildingPrefabsSection.PrefabsFileName);

        File.WriteAllText(path, "{ \"prefabs\": [ " + BuildingPrefabJson(string.Empty).Replace("\"colors\": [ { \"r\": 1, \"g\": 1, \"b\": 1, \"a\": 1 }, ", "\"colors\": [ ", StringComparison.Ordinal) + " ] }");
        Assert.Contains("$.prefabs[0].colors: expected 4 colours, found 3", Assert.Throws<DumpException>(() => BuildingPrefabsSectionReader.Read(section)).Message, StringComparison.Ordinal);

        File.WriteAllText(path, "{ \"prefabs\": [ " + BuildingPrefabJson(string.Empty) + ", " + BuildingPrefabJson(string.Empty) + " ] }");
        Assert.Contains("$.prefabs[1]: prefab index 9 is listed twice", Assert.Throws<DumpException>(() => BuildingPrefabsSectionReader.Read(section)).Message, StringComparison.Ordinal);

        File.WriteAllText(path, "{ \"prefabs\": [ " + BuildingPrefabJson(string.Empty).Replace("\"cellWidth\": 1", "\"cellWidth\": 300", StringComparison.Ordinal) + " ] }");
        Assert.Contains("$.prefabs[0].cellWidth: expected a whole number from 0 to 255", Assert.Throws<DumpException>(() => BuildingPrefabsSectionReader.Read(section)).Message, StringComparison.Ordinal);

        File.WriteAllText(path, "{ \"prefabs\": [ " + BuildingPrefabJson("\"lodMesh\": { \"min\": { \"x\": 0, \"y\": 0 }, \"max\": { \"x\": 1, \"y\": 1, \"z\": 1 } },") + " ] }");
        Assert.Contains("$.prefabs[0].lodMesh.min.z: this required property is missing", Assert.Throws<DumpException>(() => BuildingPrefabsSectionReader.Read(section)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparesAndDescribesColoursAndBoxes()
    {
        using var scope = new CultureScope("lv-LV");
        var box = new DumpBounds(new DumpVector3(-8, 0, -0.5f), new DumpVector3(8, 12.5f, 0.5f));

        Assert.Equal("(1, 0.5, 0.25, 0)", new DumpColor(1, 0.5f, 0.25f, 0).ToString());
        Assert.Equal(new DumpColor(0, 1, 1, 1), new DumpColor(-0f, 1, 1, 1));
        Assert.NotEqual(new DumpColor(0, 1, 1, 1), new DumpColor(0, 1, 1, 0.5f));
        Assert.False(new DumpColor(0, float.PositiveInfinity, 0, 0).IsFinite);
        Assert.Equal("(-8, 0, -0.5) to (8, 12.5, 0.5)", box.ToString());
        Assert.True(box == new DumpBounds(box.Min, box.Max));
        Assert.True(box != new DumpBounds(box.Min, box.Min));
        Assert.False(new DumpBounds(box.Min, new DumpVector3(float.NaN, 0, 0)).IsFinite);
        Assert.Equal(box.GetHashCode(), new DumpBounds(box.Min, box.Max).GetHashCode());
    }

    private static BuildingRecord Building(uint id) => new()
    {
        Id = id,
        Flags = 0x8010001,
        Flags2 = 0x4,
        Prefab = 312,
        Position = new DumpVector3(-1200.5f, 61.25f, 812.75f),
        Angle = 1.5f,
        MeshPosition = new DumpVector3(-1196.5f, 61.25f, 810.25f),
        ParentBuilding = 12,
        SubBuilding = 13,
        Color = new DumpColor(0.25f, 0.5f, 0.75f, 1),
        Width = 3,
        Length = 4,
        Level = 2,
        BaseHeight = 9,
    };

    private static PropRecord Prop(uint id) => new()
    {
        Id = id,
        Prefab = 77,
        Position = new DumpVector3(300.25f, 60.5f, -42.75f),
        Angle = 0.75f,
        Scale = 1.25f,
        Color = new DumpColor(1, 0.5f, 0.25f, 0),
        Flags = 0x21,
    };

    private static TreeRecord Tree(uint id) => new()
    {
        Id = id,
        Prefab = 5,
        Position = new DumpVector3(-8000.5f, 120.25f, 7999.75f),
        Scale = 1.125f,
        Brightness = 0.875f,
        Flags = 0xF21,
    };

    private static BuildingPrefabRecord BuildingPrefab(uint index, string name)
    {
        var prefab = new BuildingPrefabRecord
        {
            Index = index,
            Name = name,
            Class = new DumpItemClass { Name = "Low Residential - Level1", Service = "Residential", SubService = "ResidentialLow", Level = "Level1", Layer = 1 },
            AiType = "ResidentialBuildingAI",
            CellWidth = 2,
            CellLength = 3,
            Size = new DumpVector3(16, 9.5f, 24),
            Mesh = new DumpBounds(new DumpVector3(-8, -0.5f, -12), new DumpVector3(8, 9.5f, 12)),
            LodMesh = new DumpBounds(new DumpVector3(-7.75f, 0, -11.5f), new DumpVector3(7.75f, 9.25f, 11.5f)),
            PlacementMode = "Roadside",
            ZoningMode = "Straight",
            ExpandFrontYard = true,
            FlattenTerrain = true,
            FullGravel = true,
            UseColorVariations = true,
        };
        prefab.Colors[0] = new DumpColor(1, 0.5f, 0.25f, 0);
        prefab.Colors[1] = new DumpColor(0.5f, 0.5f, 0.25f, 0);
        prefab.Colors[2] = new DumpColor(0.25f, 0.5f, 0.25f, 0);
        prefab.Colors[3] = new DumpColor(0.125f, 0.5f, 0.25f, 0);
        prefab.SubBuildings.Add(new BuildingSubBuilding { Prefab = 313, Name = "Sub", Position = new DumpVector3(4, 0, -8), AngleDegrees = 90, FixedHeight = true });
        prefab.SubBuildings.Add(new BuildingSubBuilding { Prefab = null, Name = string.Empty, Position = new DumpVector3(0, 0, 0) });
        return prefab;
    }

    private static PropPrefabRecord PropPrefab(uint index, string name)
    {
        var prefab = new PropPrefabRecord
        {
            Index = index,
            Name = name,
            Class = new DumpItemClass { Name = "Beautification Item", Service = "Beautification", SubService = "None", Level = "Level1", Layer = 1 },
            Mesh = new DumpBounds(new DumpVector3(-1, 0, -0.5f), new DumpVector3(1, 1, 0.5f)),
            LodMesh = new DumpBounds(new DumpVector3(-1, 0, -0.5f), new DumpVector3(1, 0.75f, 0.5f)),
            MinScale = 0.75f,
            MaxScale = 1.25f,
            RequireWaterMap = true,
            LightEffects = true,
        };
        for (int i = 0; i < prefab.Colors.Length; i++)
        {
            prefab.Colors[i] = new DumpColor(1, 1, 1, 1);
        }

        return prefab;
    }

    private static TreePrefabRecord TreePrefab(uint index, string name) => new()
    {
        Index = index,
        Name = name,
        Class = new DumpItemClass { Name = "Beautification Item", Service = "Beautification", SubService = "None", Level = "Level1", Layer = 1 },
        Mesh = new DumpBounds(new DumpVector3(-3.5f, 0, -3.25f), new DumpVector3(3.5f, 12.75f, 3.25f)),
        MinScale = 0.5f,
        MaxScale = 1.5f,
        MinBrightness = 0.75f,
        MaxBrightness = 1.25f,
        DefaultColor = new DumpColor(0.25f, 0.5f, 0.125f, 1),
        CreateRuining = true,
    };

    private static string BuildingPrefabJson(string extra) =>
        """
        {
          "index": 9, "name": "Shed",
        """ + extra + """
          "class": { "name": "Shed", "service": "Industrial", "subService": "IndustrialGeneric", "level": "Level1", "layer": 1 },
          "ai": { "type": "IndustrialBuildingAI" },
          "cellWidth": 1, "cellLength": 1,
          "size": { "x": 8, "y": 4, "z": 8 },
          "placementMode": "Roadside", "zoningMode": "Straight", "circular": false, "expandFrontYard": false,
          "terrain": { "flattenTerrain": true, "flattenFullArea": false, "fullPavement": false, "fullGravel": false, "clipTerrain": false },
          "requireHeightMap": false, "useColorVariations": false,
          "colors": [ { "r": 1, "g": 1, "b": 1, "a": 1 }, { "r": 1, "g": 1, "b": 1, "a": 1 }, { "r": 1, "g": 1, "b": 1, "a": 1 }, { "r": 1, "g": 1, "b": 1, "a": 1 } ],
          "subBuildings": [],
          "futureProperty": { "anything": [ 1, 2 ] }
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
