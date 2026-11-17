using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using CsToBeamNG.Converter.Development.GeometryProbe;
using CsToBeamNG.Converter.Networks;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Development;

public sealed class GeometryProbeCheckTests
{
    // Rows of a straight 16 m wide, 64 m long road along +Z (control points at thirds), V 0..4.
    private const string Left = "-8,-8,-8,-8, 0,0,0,0, -32,-10.666667,10.666667,32, 0,1.3333334,2.6666667,4";
    private const string Right = "8,8,8,8, 0,0,0,0, -32,-10.666667,10.666667,32, 0,1.3333334,2.6666667,4";

    [Fact]
    public void MatchesWhenTheGameOutputIsTheBentMesh()
    {
        using var temp = new TempFolder();
        string path = WriteProbe(temp, outputX: 4f);

        GeometryProbeFile probe = GeometryProbeFile.Read(path);
        IReadOnlyList<ProbeComparison> comparisons = GeometryProbeCheck.Compare(probe);

        Assert.Equal("1.21.1-f9", probe.GameVersion);
        ProbeObject probed = Assert.Single(probe.Objects);
        Assert.Equal("straight-segment", probed.Label);
        Assert.Equal(2, comparisons.Count);
        Assert.All(comparisons, c => Assert.True(c.IsMatch, c.Name + ": " + c.Verdict));
    }

    [Fact]
    public void ReportsAVertexThatIsTooFar()
    {
        using var temp = new TempFolder();
        string path = WriteProbe(temp, outputX: 4.01f);

        IReadOnlyList<ProbeComparison> comparisons = GeometryProbeCheck.Compare(GeometryProbeFile.Read(path));

        ProbeComparison piece = comparisons.First(c => c.Name.EndsWith("(lod)", System.StringComparison.Ordinal));
        Assert.False(piece.IsMatch);
        Assert.Equal(0.01f, piece.MaxPositionError, 4);
        Assert.Contains("too far", GeometryProbeCheck.FormatTable(comparisons), System.StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOtherFiles()
    {
        using var temp = new TempFolder();
        string path = temp.PathOf("other.json");
        File.WriteAllText(path, "{\"format\":\"something-else\",\"version\":1}");

        Assert.Throws<InvalidDataException>(() => GeometryProbeFile.Read(path));
    }

    [Fact]
    public void WritesObjWithOneBasedIndicesAcrossObjects()
    {
        var mesh = new NetMeshData([Vector3.Zero, Vector3.UnitX, Vector3.UnitZ], null, null, null, null, [0, 1, 2]);
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        GeometryProbeCheck.WriteObj(writer, [("a", mesh), ("b c", mesh)]);

        string obj = writer.ToString();
        Assert.Contains("o a\nv 0 0 0\nv 1 0 0\nv 0 0 1\nf 1 2 3\n", obj, System.StringComparison.Ordinal);
        Assert.Contains("o b_c\n", obj, System.StringComparison.Ordinal);
        Assert.EndsWith("f 4 5 6\n", obj, System.StringComparison.Ordinal);
    }

    // A probe of one straight segment: its mesh has one vertex at x = 4 on the middle line, so the game's
    // output is the same point; outputX lets a test move it.
    private static string WriteProbe(TempFolder temp, float outputX)
    {
        string x = outputX.ToString(CultureInfo.InvariantCulture);
        var json = new StringBuilder();
        json.Append("{\"format\":\"cstobeamng-geometry-probe\",\"version\":1,\"game\":\"1.21.1-f9\",");
        json.Append("\"objects\":[{\"label\":\"straight-segment\",\"kind\":\"segment\",\"id\":7,\"reason\":\"test\",\"info\":\"Basic Road\",");
        json.Append("\"groupPosition\":[0,0,0],\"layers\":[{\"layer\":9,");
        json.Append("\"game\":{\"vertexCount\":1,\"vertices\":[" + x + ",0,0],\"triangles\":[]},");
        json.Append("\"pieces\":[{\"kind\":\"segment\",\"info\":\"Basic Road\",\"meshIndex\":0,\"segment\":7,\"turnAround\":false,");
        json.Append("\"requireHeightMap\":false,\"sourceData\":0,\"mainMesh\":-1,");
        json.Append("\"left\":[" + Left + "],\"right\":[" + Right + "],\"meshScale\":[0.0625,0.015625,1,1],");
        json.Append("\"output\":{\"vertexCount\":1,\"vertices\":[" + x + ",0,0],\"normals\":[0,1,0],\"triangles\":[]}}]}]}],");
        json.Append("\"meshes\":[{\"id\":0,\"role\":\"lodData\",\"type\":\"meshData\",\"vertexCount\":1,\"vertices\":[4,0,0],\"normals\":[0,1,0],\"triangles\":[]}]}");
        string path = temp.PathOf("geometry-probe.json");
        File.WriteAllText(path, json.ToString());
        return path;
    }
}
