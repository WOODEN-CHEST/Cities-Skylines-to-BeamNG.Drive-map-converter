using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Xml;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>
/// Writes a <see cref="ColladaShape"/> as a Collada 1.4.1 file in the layout of the game's own meshes
/// (docs/research/beamng-level-format.md, Meshes).
/// </summary>
/// <remarks>
/// <para>The node tree:</para>
/// <code>
/// base00
///   collision-1          (only with a collision mesh: declares the collision detail level)
///   start01
///     &lt;name&gt;_a&lt;size&gt;   (one node per level of detail)
///     Colmesh-1          (the collision mesh)
/// </code>
/// <para>
/// Each mesh's material binds by the Collada material's <c>name</c>, which must equal a <see cref="ShapeMaterial"/>'s
/// <c>mapTo</c>. The Collada effects are plain grey; the game uses the materials file instead. The asset dates are
/// fixed, so the same shape always gives the same file.
/// </para>
/// </remarks>
public static class ColladaFile
{
    /// <summary>The node name of the collision mesh.</summary>
    public const string CollisionMeshNodeName = "Colmesh-1";

    private const string ColladaNamespace = "http://www.collada.org/2005/11/COLLADASchema";
    private const string FixedTimestamp = "2000-01-01T00:00:00Z";
    private const string IdentityMatrix = "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1";

    /// <summary>Creates the file.</summary>
    /// <param name="path">Where the file goes, such as <c>/levels/x/art/shapes/box.dae</c>.</param>
    /// <param name="shape">The shape.</param>
    /// <returns>The file.</returns>
    public static OutputFile Create(VirtualPath path, ColladaShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return new OutputFile(path, stream => Write(stream, shape));
    }

    /// <summary>The node name of a level of detail, such as <c>box_a100</c>.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="detail">One of its levels of detail.</param>
    /// <returns>The node name.</returns>
    public static string GetDetailNodeName(ColladaShape shape, ShapeDetail detail)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(detail);
        return shape.Name + "_a" + detail.PixelSize.ToString(CultureInfo.InvariantCulture);
    }

    private static void Write(Stream stream, ColladaShape shape)
    {
        var meshes = new List<(string NodeName, ShapeMesh Mesh)>();
        foreach (ShapeDetail detail in shape.Details)
        {
            meshes.Add((GetDetailNodeName(shape, detail), detail.Mesh));
        }

        if (shape.CollisionMesh is not null)
        {
            meshes.Add((CollisionMeshNodeName, shape.CollisionMesh));
        }

        var materials = new List<string>();
        foreach ((_, ShapeMesh mesh) in meshes)
        {
            if (!materials.Contains(mesh.MaterialName))
            {
                materials.Add(mesh.MaterialName);
            }
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            CloseOutput = false,
        };
        using (var xml = XmlWriter.Create(stream, settings))
        {
            xml.WriteStartDocument();
            Start(xml, "COLLADA");
            xml.WriteAttributeString("version", "1.4.1");
            WriteAsset(xml);
            WriteEffectsAndMaterials(xml, materials);
            Start(xml, "library_geometries");
            foreach ((string nodeName, ShapeMesh mesh) in meshes)
            {
                WriteGeometry(xml, nodeName, mesh);
            }

            xml.WriteEndElement();
            WriteVisualScene(xml, meshes, hasCollision: shape.CollisionMesh is not null);
            Start(xml, "scene");
            Start(xml, "instance_visual_scene");
            xml.WriteAttributeString("url", "#Scene");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        stream.WriteByte((byte)'\n');
    }

    private static void WriteAsset(XmlWriter xml)
    {
        Start(xml, "asset");
        Start(xml, "contributor");
        Element(xml, "authoring_tool", "CsToBeamNG");
        xml.WriteEndElement();
        Element(xml, "created", FixedTimestamp);
        Element(xml, "modified", FixedTimestamp);
        Start(xml, "unit");
        xml.WriteAttributeString("name", "meter");
        xml.WriteAttributeString("meter", "1");
        xml.WriteEndElement();
        Element(xml, "up_axis", "Z_UP");
        xml.WriteEndElement();
    }

    private static void WriteEffectsAndMaterials(XmlWriter xml, List<string> materials)
    {
        Start(xml, "library_effects");
        foreach (string material in materials)
        {
            Start(xml, "effect");
            xml.WriteAttributeString("id", material + "-effect");
            Start(xml, "profile_COMMON");
            Start(xml, "technique");
            xml.WriteAttributeString("sid", "common");
            Start(xml, "lambert");
            Start(xml, "diffuse");
            Start(xml, "color");
            xml.WriteAttributeString("sid", "diffuse");
            xml.WriteString("0.8 0.8 0.8 1");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        Start(xml, "library_materials");
        foreach (string material in materials)
        {
            Start(xml, "material");
            xml.WriteAttributeString("id", MaterialId(material));
            xml.WriteAttributeString("name", material);
            Start(xml, "instance_effect");
            xml.WriteAttributeString("url", "#" + material + "-effect");
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteGeometry(XmlWriter xml, string nodeName, ShapeMesh mesh)
    {
        string geometryId = nodeName + "-mesh";
        Start(xml, "geometry");
        xml.WriteAttributeString("id", geometryId);
        xml.WriteAttributeString("name", nodeName);
        Start(xml, "mesh");

        var positions = new List<float>();
        foreach (Vector3 position in mesh.Positions)
        {
            positions.AddRange([position.X, position.Y, position.Z]);
        }

        var normals = new List<float>();
        foreach (Vector3 normal in mesh.Normals)
        {
            normals.AddRange([normal.X, normal.Y, normal.Z]);
        }

        var textureCoordinates = new List<float>();
        foreach (Vector2 uv in mesh.TextureCoordinates)
        {
            textureCoordinates.AddRange([uv.X, uv.Y]);
        }

        WriteSource(xml, geometryId + "-positions", positions, ["X", "Y", "Z"]);
        WriteSource(xml, geometryId + "-normals", normals, ["X", "Y", "Z"]);
        WriteSource(xml, geometryId + "-map-0", textureCoordinates, ["S", "T"]);

        Start(xml, "vertices");
        xml.WriteAttributeString("id", geometryId + "-vertices");
        WriteInput(xml, "POSITION", geometryId + "-positions", offset: null);
        xml.WriteEndElement();

        // One index per input, as Blender's exporter writes them; every input uses the vertex's own index.
        Start(xml, "triangles");
        xml.WriteAttributeString("material", MaterialId(mesh.MaterialName));
        xml.WriteAttributeString("count", mesh.TriangleCount.ToString(CultureInfo.InvariantCulture));
        WriteInput(xml, "VERTEX", geometryId + "-vertices", offset: 0);
        WriteInput(xml, "NORMAL", geometryId + "-normals", offset: 1);
        WriteInput(xml, "TEXCOORD", geometryId + "-map-0", offset: 2, set: 0);
        var indices = new StringBuilder();
        foreach (int index in mesh.Indices)
        {
            string text = index.ToString(CultureInfo.InvariantCulture);
            indices.Append(indices.Length == 0 ? string.Empty : " ").Append(text).Append(' ').Append(text).Append(' ').Append(text);
        }

        Element(xml, "p", indices.ToString());
        xml.WriteEndElement();

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteSource(XmlWriter xml, string id, List<float> values, string[] parameters)
    {
        Start(xml, "source");
        xml.WriteAttributeString("id", id);
        Start(xml, "float_array");
        xml.WriteAttributeString("id", id + "-array");
        xml.WriteAttributeString("count", values.Count.ToString(CultureInfo.InvariantCulture));
        var text = new StringBuilder();
        foreach (float value in values)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(FormatFloat(value));
        }

        xml.WriteString(text.ToString());
        xml.WriteEndElement();
        Start(xml, "technique_common");
        Start(xml, "accessor");
        xml.WriteAttributeString("source", "#" + id + "-array");
        xml.WriteAttributeString("count", (values.Count / parameters.Length).ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("stride", parameters.Length.ToString(CultureInfo.InvariantCulture));
        foreach (string parameter in parameters)
        {
            Start(xml, "param");
            xml.WriteAttributeString("name", parameter);
            xml.WriteAttributeString("type", "float");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteInput(XmlWriter xml, string semantic, string source, int? offset, int? set = null)
    {
        Start(xml, "input");
        xml.WriteAttributeString("semantic", semantic);
        xml.WriteAttributeString("source", "#" + source);
        if (offset is int offsetValue)
        {
            xml.WriteAttributeString("offset", offsetValue.ToString(CultureInfo.InvariantCulture));
        }

        if (set is int setValue)
        {
            xml.WriteAttributeString("set", setValue.ToString(CultureInfo.InvariantCulture));
        }

        xml.WriteEndElement();
    }

    private static void WriteVisualScene(XmlWriter xml, List<(string NodeName, ShapeMesh Mesh)> meshes, bool hasCollision)
    {
        Start(xml, "library_visual_scenes");
        Start(xml, "visual_scene");
        xml.WriteAttributeString("id", "Scene");
        xml.WriteAttributeString("name", "Scene");
        StartNode(xml, "base00");
        if (hasCollision)
        {
            StartNode(xml, "collision-1");
            xml.WriteEndElement();
        }

        StartNode(xml, "start01");
        foreach ((string nodeName, ShapeMesh mesh) in meshes)
        {
            StartNode(xml, nodeName);
            Start(xml, "instance_geometry");
            xml.WriteAttributeString("url", "#" + nodeName + "-mesh");
            xml.WriteAttributeString("name", nodeName);
            Start(xml, "bind_material");
            Start(xml, "technique_common");
            Start(xml, "instance_material");
            xml.WriteAttributeString("symbol", MaterialId(mesh.MaterialName));
            xml.WriteAttributeString("target", "#" + MaterialId(mesh.MaterialName));
            Start(xml, "bind_vertex_input");
            xml.WriteAttributeString("semantic", "UVMap");
            xml.WriteAttributeString("input_semantic", "TEXCOORD");
            xml.WriteAttributeString("input_set", "0");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void StartNode(XmlWriter xml, string name)
    {
        Start(xml, "node");
        xml.WriteAttributeString("id", name);
        xml.WriteAttributeString("name", name);
        xml.WriteAttributeString("type", "NODE");
        Start(xml, "matrix");
        xml.WriteAttributeString("sid", "transform");
        xml.WriteString(IdentityMatrix);
        xml.WriteEndElement();
    }

    private static string MaterialId(string material) => material + "-material";

    private static string FormatFloat(float value) =>
        value == 0 ? "0" : value.ToString(CultureInfo.InvariantCulture);

    private static void Start(XmlWriter xml, string name) => xml.WriteStartElement(name, ColladaNamespace);

    private static void Element(XmlWriter xml, string name, string value)
    {
        Start(xml, name);
        xml.WriteString(value);
        xml.WriteEndElement();
    }
}
