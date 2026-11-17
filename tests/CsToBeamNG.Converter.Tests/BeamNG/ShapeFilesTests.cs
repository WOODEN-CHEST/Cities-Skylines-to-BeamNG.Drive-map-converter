using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Shapes;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class ShapeFilesTests
{
    private static readonly XNamespace _collada = "http://www.collada.org/2005/11/COLLADASchema";

    [Fact]
    public void BuildsABoxWithOutwardFacingTriangles()
    {
        var size = new Vector3(8, 6, 4);
        ShapeMesh box = ShapeMeshes.Box(size, "test_box");

        Assert.Equal(24, box.Positions.Count);
        Assert.Equal(12, box.TriangleCount);
        Assert.Equal(new Vector3(-4, -3, 0), box.Positions.Aggregate(Vector3.Min));
        Assert.Equal(new Vector3(4, 3, 4), box.Positions.Aggregate(Vector3.Max));

        var centre = new Vector3(0, 0, size.Z / 2);
        for (int i = 0; i < box.Indices.Count; i += 3)
        {
            Vector3 a = box.Positions[box.Indices[i]];
            Vector3 b = box.Positions[box.Indices[i + 1]];
            Vector3 c = box.Positions[box.Indices[i + 2]];
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

            // Counter-clockwise seen from outside: the winding's normal points away from the centre and matches
            // the vertex normal.
            Assert.True(Vector3.Dot(normal, ((a + b + c) / 3) - centre) > 0);
            Assert.Equal(box.Normals[box.Indices[i]], normal);
        }
    }

    [Fact]
    public void WritesTheGamesNodeLayout()
    {
        XDocument document = XDocument.Parse(CreateBoxFile(withCollision: true).Text());

        XElement baseNode = Node(document.Root!.Element(_collada + "library_visual_scenes")!.Element(_collada + "visual_scene")!, "base00");
        Assert.Equal(["collision-1", "start01"], ChildNodeNames(baseNode));
        Assert.Equal(["test_box_a100", "test_box_a10", "Colmesh-1"], ChildNodeNames(Node(baseNode, "start01")));
        Assert.Equal("Z_UP", document.Root.Element(_collada + "asset")!.Element(_collada + "up_axis")!.Value);

        XElement material = document.Root.Element(_collada + "library_materials")!.Element(_collada + "material")!;
        Assert.Equal("test_box", material.Attribute("name")!.Value);
        Assert.Single(document.Root.Element(_collada + "library_materials")!.Elements());
    }

    [Fact]
    public void LeavesOutTheCollisionDetailWithoutACollisionMesh()
    {
        XDocument document = XDocument.Parse(CreateBoxFile(withCollision: false).Text());

        XElement baseNode = Node(document.Root!.Element(_collada + "library_visual_scenes")!.Element(_collada + "visual_scene")!, "base00");
        Assert.Equal(["start01"], ChildNodeNames(baseNode));
    }

    [Fact]
    public void WritesGeometryThatMatchesTheMesh()
    {
        XDocument document = XDocument.Parse(CreateBoxFile(withCollision: true).Text());
        XElement geometry = document.Descendants(_collada + "geometry").First(element => element.Attribute("id")!.Value == "test_box_a100-mesh");

        XElement positions = geometry.Descendants(_collada + "float_array").First();
        Assert.Equal("72", positions.Attribute("count")!.Value);
        Assert.StartsWith("4 -3 0 4 3 0 4 3 4 ", positions.Value, StringComparison.Ordinal);

        XElement triangles = geometry.Descendants(_collada + "triangles").Single();
        Assert.Equal("12", triangles.Attribute("count")!.Value);
        Assert.Equal("test_box-material", triangles.Attribute("material")!.Value);
        Assert.Equal(["0", "1", "2"], triangles.Elements(_collada + "input").Select(input => input.Attribute("offset")!.Value));
        Assert.Equal(12 * 3 * 3, triangles.Element(_collada + "p")!.Value.Split(' ').Length);
    }

    [Fact]
    public void WritesTheSameBytesUnderADecimalCommaCulture()
    {
        var size = new Vector3(1.25f, 2.5f, 0.125f);
        byte[] expected = CreateBoxFile(withCollision: true, size).ToBytes();
        using (new CultureScope("lv-LV"))
        {
            Assert.Equal(expected, CreateBoxFile(withCollision: true, size).ToBytes());
        }

        Assert.Contains("0.625 -1.25 0", CreateBoxFile(withCollision: true, size).Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidShapes()
    {
        ShapeMesh mesh = ShapeMeshes.Box(Vector3.One, "test_box");

        Assert.Throws<ArgumentException>(() => new ColladaShape { Name = "x", Details = [] });
        Assert.Throws<ArgumentException>(() => new ColladaShape { Name = "x", Details = [new ShapeDetail(mesh, 10), new ShapeDetail(mesh, 10)] });
        Assert.Throws<ArgumentException>(() => new ColladaShape { Name = "bad name", Details = [new ShapeDetail(mesh, 10)] });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShapeDetail(mesh, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeMeshes.Box(new Vector3(1, 0, 1), "test_box"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShapeMesh("m", [Vector3.Zero], [Vector3.UnitZ], [Vector2.Zero], [0, 0, 1]));
        Assert.Throws<ArgumentException>(() => new ShapeMesh("m", [Vector3.Zero], [Vector3.UnitZ], [Vector2.Zero], [0, 0]));
    }

    [Fact]
    public void WritesMaterialsLikeTheGamesOwnFiles()
    {
        OutputFile file = MaterialsFile.Create(
            VirtualPath.Parse("/levels/test_level/art/shapes/main.materials.json"),
            "test_level",
            [new ShapeMaterial { Name = "test_box", BaseColorFactor = new Vector4(0.9f, 0.45f, 0.1f, 1), RoughnessFactor = 0.6f, MetallicFactor = 0 }]);

        // The persistent id is Python's uuid.uuid5 of "test_level:material:test_box".
        Assert.Equal(
            """
            {
              "test_box": {
                "name": "test_box",
                "mapTo": "test_box",
                "class": "Material",
                "persistentId": "cb630184-f8a0-509b-84cf-257bea45ec5b",
                "Stages": [
                  {
                    "baseColorFactor": [
                      0.9,
                      0.45,
                      0.1,
                      1
                    ],
                    "metallicFactor": 0,
                    "roughnessFactor": 0.6
                  },
                  {},
                  {},
                  {}
                ],
                "version": 1.5
              }
            }

            """,
            file.Text());
    }

    [Fact]
    public void RejectsMaterialFilesTheGameWouldntLoad()
    {
        var material = new ShapeMaterial { Name = "test_box" };

        Assert.Throws<ArgumentException>(() => MaterialsFile.Create(VirtualPath.Parse("/levels/x/art/box.json"), "x", [material]));
        Assert.Throws<ArgumentException>(() => MaterialsFile.Create(VirtualPath.Parse("/levels/x/main.materials.json"), "x", [material, material]));
    }

    private static OutputFile CreateBoxFile(bool withCollision, Vector3? size = null)
    {
        ShapeMesh mesh = ShapeMeshes.Box(size ?? new Vector3(8, 6, 4), "test_box");
        var shape = new ColladaShape
        {
            Name = "test_box",
            Details = [new ShapeDetail(mesh, 100), new ShapeDetail(mesh, 10)],
            CollisionMesh = withCollision ? mesh : null,
        };
        return ColladaFile.Create(VirtualPath.Parse("/levels/test_level/art/shapes/box.dae"), shape);
    }

    private static XElement Node(XElement parent, string name) =>
        parent.Elements(_collada + "node").Single(node => node.Attribute("name")!.Value == name);

    private static IEnumerable<string> ChildNodeNames(XElement node) =>
        node.Elements(_collada + "node").Select(child => child.Attribute("name")!.Value);
}
