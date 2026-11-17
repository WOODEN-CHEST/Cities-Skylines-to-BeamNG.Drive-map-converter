using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using CsToBeamNG.Converter.Networks;

namespace CsToBeamNG.Converter.Development.GeometryProbe;

/// <summary>
/// Checks <see cref="NetMeshBending"/> against the game: bends each probed piece's meshes with the probe's
/// inputs and compares the result with the game's own output, vertex by vertex. Also checks that the pieces
/// together make up the game's output for the whole object, which tests the exporter's computation of the
/// inputs (plan step 0.7; docs/research/cs1-networks.md).
/// </summary>
public static class GeometryProbeCheck
{
    /// <summary>The largest position error that counts as matching: 1 mm.</summary>
    public const float PositionTolerance = 0.001f;

    /// <summary>Compares every piece of a probe file.</summary>
    /// <param name="probe">The probe.</param>
    public static IReadOnlyList<ProbeComparison> Compare(GeometryProbeFile probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var results = new List<ProbeComparison>();
        foreach (ProbeObject probed in probe.Objects)
        {
            foreach (ProbeLayer layer in probed.Layers)
            {
                var pieceOutputs = new List<NetMeshData>();
                for (int i = 0; i < layer.Pieces.Count; i++)
                {
                    ProbePiece piece = layer.Pieces[i];
                    string name = $"{probed.Label} L{layer.Layer.ToString(CultureInfo.InvariantCulture)} #{i.ToString(CultureInfo.InvariantCulture)} {piece.Kind} {piece.Info}";
                    NetMeshData? lodSource = MeshData(probe, piece.SourceData);
                    if (lodSource is not null && piece.Output is not null)
                    {
                        results.Add(Measure(name + " (lod)", Bend(piece, lodSource), piece.Output, piece.RequireHeightMap));
                        pieceOutputs.Add(piece.Output);
                    }

                    NetMeshData? mainSource = MeshData(probe, piece.MainMesh);
                    if (mainSource is not null && piece.MainOutput is not null)
                    {
                        results.Add(Measure(name + " (main)", Bend(piece, mainSource), piece.MainOutput, piece.RequireHeightMap));
                    }

                    if (mainSource is not null && piece.Gpu is { MainOutput: { } gpuOutput } gpu)
                    {
                        results.Add(Measure(name + " (main, renderer inputs)", BendJunction(gpu, mainSource), gpuOutput, heightFromTerrain: false));
                    }
                }

                if (layer.Game is not null)
                {
                    results.Add(MeasureConcatenation($"{probed.Label} L{layer.Layer.ToString(CultureInfo.InvariantCulture)} pieces = game", pieceOutputs, layer.Game));
                }
            }
        }

        return results;
    }

    /// <summary>Bends a mesh with a piece's inputs, choosing the function by the piece's kind.</summary>
    /// <param name="piece">The piece.</param>
    /// <param name="mesh">The straight mesh: the piece's level-of-detail data or its main mesh.</param>
    public static NetMeshData Bend(ProbePiece piece, NetMeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return piece.Kind switch
        {
            "segment" or "bendSegment" => NetMeshBending.BendSegment(mesh, piece.Left, piece.Right, piece.MeshScale),
            "bendNode" => NetMeshBending.BendNodeMesh(mesh, piece.Left, piece.Right, piece.MeshScale),
            "junction" or "end" => NetMeshBending.BendJunction(mesh, piece.Left, piece.Right, piece.LeftB, piece.RightB, piece.MeshScale, piece.CenterPos, piece.SideScale),
            _ => throw new InvalidDataException($"Unknown piece kind '{piece.Kind}'."),
        };
    }

    /// <summary>Bends a junction mesh with the renderer's inputs.</summary>
    /// <param name="gpu">The renderer's inputs.</param>
    /// <param name="mesh">The straight mesh.</param>
    public static NetMeshData BendJunction(ProbeGpuInputs gpu, NetMeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        return NetMeshBending.BendJunction(mesh, gpu.Left, gpu.Right, gpu.LeftB, gpu.RightB, gpu.MeshScale, gpu.CenterPos, gpu.SideScale);
    }

    /// <summary>
    /// Compares the two ways the game shapes junctions: for each junction piece with a readable main mesh,
    /// the largest distance between the mesh bent with the level-of-detail inputs and with the renderer's
    /// inputs, both relative to the node. Not a check: they are expected to differ.
    /// </summary>
    /// <param name="probe">The probe.</param>
    public static IReadOnlyList<(string Name, float MaxDistance, float MeanDistance)> CompareLodWithRenderer(GeometryProbeFile probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var results = new List<(string, float, float)>();
        foreach (ProbeObject probed in probe.Objects)
        {
            foreach (ProbeLayer layer in probed.Layers)
            {
                for (int i = 0; i < layer.Pieces.Count; i++)
                {
                    ProbePiece piece = layer.Pieces[i];
                    if (piece.MainOutput is not { } lodShape || piece.Gpu is not { MainOutput: { } gpuShape } gpu || lodShape.VertexCount != gpuShape.VertexCount)
                    {
                        continue;
                    }

                    Vector3 shift = gpu.Position - probed.GroupPosition;
                    float max = 0f;
                    double sum = 0;
                    for (int v = 0; v < lodShape.VertexCount; v++)
                    {
                        float distance = (gpuShape.Vertices[v] + shift - lodShape.Vertices[v]).Length();
                        max = MathF.Max(max, distance);
                        sum += distance;
                    }

                    results.Add(($"{probed.Label} #{i.ToString(CultureInfo.InvariantCulture)} {piece.Info}", max, (float)(sum / lodShape.VertexCount)));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Compares the renderer's own instance data, where the probe found some, with the inputs the probe
    /// computed for the same pieces: the largest absolute difference of any matrix or vector element.
    /// </summary>
    /// <param name="probe">The probe.</param>
    public static IReadOnlyList<(string Name, float MaxDifference)> CompareWithRenderer(GeometryProbeFile probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var results = new List<(string, float)>();
        foreach (ProbeObject probed in probe.Objects)
        {
            if (probed.Renderer.Count == 0)
            {
                continue;
            }

            // One renderer instance per segment slot, in slot order, as the junction code fills the chain.
            var computed = new SortedDictionary<int, ProbeGpuInputs>();
            foreach (ProbeLayer layer in probed.Layers)
            {
                foreach (ProbePiece piece in layer.Pieces)
                {
                    if (piece.Gpu is not null && piece.Slot >= 0)
                    {
                        computed[piece.Slot] = piece.Gpu;
                    }
                }
            }

            int index = 0;
            foreach (ProbeGpuInputs ours in computed.Values)
            {
                if (index >= probed.Renderer.Count)
                {
                    break;
                }

                ProbeGpuInputs theirs = probed.Renderer[index];
                float max = MaxDifference(ours.Left, theirs.Left);
                max = MathF.Max(max, MaxDifference(ours.Right, theirs.Right));
                max = MathF.Max(max, MaxDifference(ours.LeftB, theirs.LeftB));
                max = MathF.Max(max, MaxDifference(ours.RightB, theirs.RightB));
                max = MathF.Max(max, MaxDifference(ours.MeshScale, theirs.MeshScale));
                max = MathF.Max(max, MaxDifference(ours.CenterPos, theirs.CenterPos));
                max = MathF.Max(max, MaxDifference(ours.SideScale, theirs.SideScale));
                max = MathF.Max(max, (ours.Position - theirs.Position).Length());
                results.Add(($"{probed.Label} instance {index.ToString(CultureInfo.InvariantCulture)}", max));
                index++;
            }
        }

        return results;
    }

    private static float MaxDifference(NetControlMatrix a, NetControlMatrix b) => MathF.Max(
        MathF.Max(MaxDifference(new Vector4(a.A, 0f), new Vector4(b.A, 0f)), MaxDifference(new Vector4(a.B, 0f), new Vector4(b.B, 0f))),
        MathF.Max(
            MathF.Max(MaxDifference(new Vector4(a.C, 0f), new Vector4(b.C, 0f)), MaxDifference(new Vector4(a.D, 0f), new Vector4(b.D, 0f))),
            MaxDifference(a.V, b.V)));

    private static float MaxDifference(Vector4 a, Vector4 b)
    {
        Vector4 d = Vector4.Abs(a - b);
        return MathF.Max(MathF.Max(d.X, d.Y), MathF.Max(d.Z, d.W));
    }

    /// <summary>Writes a reconstruction as Wavefront OBJ, in game coordinates relative to the object's group position.</summary>
    /// <param name="writer">Receives the text.</param>
    /// <param name="meshes">The meshes, each written as its own object.</param>
    public static void WriteObj(TextWriter writer, IReadOnlyList<(string Name, NetMeshData Mesh)> meshes)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(meshes);
        int offset = 1;
        foreach ((string name, NetMeshData mesh) in meshes)
        {
            writer.Write("o " + name.Replace(' ', '_') + "\n");
            foreach (Vector3 v in mesh.Vertices)
            {
                writer.Write(string.Create(CultureInfo.InvariantCulture, $"v {v.X} {v.Y} {v.Z}\n"));
            }

            for (int i = 0; i + 2 < mesh.Triangles.Length; i += 3)
            {
                writer.Write(string.Create(CultureInfo.InvariantCulture, $"f {mesh.Triangles[i] + offset} {mesh.Triangles[i + 1] + offset} {mesh.Triangles[i + 2] + offset}\n"));
            }

            offset += mesh.VertexCount;
        }
    }

    /// <summary>Formats the comparisons as a table.</summary>
    /// <param name="comparisons">The comparisons.</param>
    public static string FormatTable(IReadOnlyList<ProbeComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{"piece",-82} {"verts",6} {"max pos m",10} {"mean pos m",10} {"max nrm",9} {"max uv",9}  result\n");
        foreach (ProbeComparison c in comparisons)
        {
            text.Append(CultureInfo.InvariantCulture, $"{c.Name,-82} {c.VertexCount,6} {c.MaxPositionError,10:0.000000} {c.MeanPositionError,10:0.000000} {c.MaxNormalError,9:0.000000} {c.MaxUvError,9:0.000000}  {c.Verdict}\n");
        }

        return text.ToString();
    }

    private static NetMeshData? MeshData(GeometryProbeFile probe, int id) =>
        id >= 0 && id < probe.Meshes.Count ? probe.Meshes[id].Data : null;

    private static ProbeComparison Measure(string name, NetMeshData ours, NetMeshData game, bool heightFromTerrain)
    {
        if (ours.VertexCount != game.VertexCount)
        {
            return new ProbeComparison(name, game.VertexCount, float.NaN, float.NaN, float.NaN, float.NaN, $"vertex counts differ: {ours.VertexCount} vs {game.VertexCount}");
        }

        float maxPosition = 0f;
        double sumPosition = 0;
        float maxNormal = 0f;
        float maxUv = 0f;
        for (int i = 0; i < game.VertexCount; i++)
        {
            Vector3 difference = ours.Vertices[i] - game.Vertices[i];
            if (heightFromTerrain)
            {
                difference.Y = 0f;
            }

            float error = difference.Length();
            maxPosition = MathF.Max(maxPosition, error);
            sumPosition += error;
            if (ours.Normals is not null && game.Normals is not null)
            {
                maxNormal = MathF.Max(maxNormal, (ours.Normals[i] - game.Normals[i]).Length());
            }

            if (ours.Uvs is not null && game.Uvs is not null)
            {
                maxUv = MathF.Max(maxUv, (ours.Uvs[i] - game.Uvs[i]).Length());
            }
        }

        float mean = game.VertexCount == 0 ? 0f : (float)(sumPosition / game.VertexCount);
        string verdict = maxPosition <= PositionTolerance ? "ok" : "too far";
        if (heightFromTerrain)
        {
            verdict += " (heights from terrain not compared)";
        }

        return new ProbeComparison(name, game.VertexCount, maxPosition, mean, maxNormal, maxUv, verdict);
    }

    private static ProbeComparison MeasureConcatenation(string name, List<NetMeshData> pieces, NetMeshData game)
    {
        int total = 0;
        foreach (NetMeshData piece in pieces)
        {
            total += piece.VertexCount;
        }

        if (total != game.VertexCount)
        {
            return new ProbeComparison(name, game.VertexCount, float.NaN, float.NaN, float.NaN, float.NaN, $"vertex counts differ: pieces {total}, game {game.VertexCount}");
        }

        var vertices = new Vector3[total];
        var normals = new Vector3[total];
        var uvs = new Vector2[total];
        int at = 0;
        foreach (NetMeshData piece in pieces)
        {
            Array.Copy(piece.Vertices, 0, vertices, at, piece.VertexCount);
            piece.Normals?.CopyTo(normals, at);
            piece.Uvs?.CopyTo(uvs, at);
            at += piece.VertexCount;
        }

        return Measure(name, new NetMeshData(vertices, normals, null, uvs, null, []), game, heightFromTerrain: false);
    }
}

/// <summary>The comparison of a reconstruction with the game's output.</summary>
/// <param name="Name">What was compared.</param>
/// <param name="VertexCount">The number of vertices.</param>
/// <param name="MaxPositionError">The largest distance between corresponding vertices, in metres.</param>
/// <param name="MeanPositionError">The mean distance, in metres.</param>
/// <param name="MaxNormalError">The largest difference between corresponding normals (length of the difference vector).</param>
/// <param name="MaxUvError">The largest difference between corresponding texture coordinates.</param>
/// <param name="Verdict"><c>ok</c> when within <see cref="GeometryProbeCheck.PositionTolerance"/>, otherwise what is wrong.</param>
public sealed record ProbeComparison(
    string Name,
    int VertexCount,
    float MaxPositionError,
    float MeanPositionError,
    float MaxNormalError,
    float MaxUvError,
    string Verdict)
{
    /// <summary>Whether the positions match within the tolerance.</summary>
    public bool IsMatch => Verdict.StartsWith("ok", StringComparison.Ordinal);
}
