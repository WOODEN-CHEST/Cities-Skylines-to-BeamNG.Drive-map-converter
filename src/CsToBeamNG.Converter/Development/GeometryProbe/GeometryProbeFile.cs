using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using CsToBeamNG.Converter.Networks;

namespace CsToBeamNG.Converter.Development.GeometryProbe;

/// <summary>
/// The geometry probe file that the exporter writes on request (<c>cstobeamng export --probe geometry</c>):
/// for a few road segments and nodes, the inputs of the game's mesh-bending functions and their outputs.
/// A developer format for research (plan step 0.7), read only by <see cref="GeometryProbeCheck"/>.
/// </summary>
public sealed class GeometryProbeFile
{
    /// <summary>The format name in the file.</summary>
    public const string FormatName = "cstobeamng-geometry-probe";

    /// <summary>The format version this reader understands.</summary>
    public const int FormatVersion = 1;

    private GeometryProbeFile(string gameVersion, IReadOnlyList<ProbeObject> objects, IReadOnlyList<ProbeMesh> meshes)
    {
        GameVersion = gameVersion;
        Objects = objects;
        Meshes = meshes;
    }

    /// <summary>The game version that wrote the probe.</summary>
    public string GameVersion { get; }

    /// <summary>The probed segments and nodes.</summary>
    public IReadOnlyList<ProbeObject> Objects { get; }

    /// <summary>The meshes the pieces refer to, by index.</summary>
    public IReadOnlyList<ProbeMesh> Meshes { get; }

    /// <summary>Reads a probe file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="InvalidDataException">The file isn't a probe of this version.</exception>
    public static GeometryProbeFile Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        string format = root.GetProperty("format").GetString() ?? string.Empty;
        int version = root.GetProperty("version").GetInt32();
        if (format != FormatName || version != FormatVersion)
        {
            throw new InvalidDataException($"'{path}' is a '{format}' file of version {version}; expected {FormatName} version {FormatVersion}.");
        }

        var meshes = new List<ProbeMesh>();
        foreach (JsonElement mesh in root.GetProperty("meshes").EnumerateArray())
        {
            meshes.Add(ReadMesh(mesh));
        }

        var objects = new List<ProbeObject>();
        foreach (JsonElement element in root.GetProperty("objects").EnumerateArray())
        {
            objects.Add(ReadObject(element));
        }

        return new GeometryProbeFile(root.GetProperty("game").GetString() ?? string.Empty, objects, meshes);
    }

    private static ProbeObject ReadObject(JsonElement element)
    {
        var layers = new List<ProbeLayer>();
        foreach (JsonElement layer in element.GetProperty("layers").EnumerateArray())
        {
            var pieces = new List<ProbePiece>();
            foreach (JsonElement piece in layer.GetProperty("pieces").EnumerateArray())
            {
                pieces.Add(ReadPiece(piece));
            }

            NetMeshData? game = layer.TryGetProperty("game", out JsonElement gameElement) ? ReadMeshData(gameElement) : null;
            layers.Add(new ProbeLayer(layer.GetProperty("layer").GetInt32(), game, pieces));
        }

        var renderer = new List<ProbeGpuInputs>();
        if (element.TryGetProperty("renderer", out JsonElement rendererElement))
        {
            foreach (JsonElement instance in rendererElement.EnumerateArray())
            {
                renderer.Add(ReadGpu(instance));
            }
        }

        return new ProbeObject(
            element.GetProperty("label").GetString() ?? string.Empty,
            element.GetProperty("kind").GetString() ?? string.Empty,
            element.GetProperty("id").GetInt32(),
            element.TryGetProperty("info", out JsonElement info) ? info.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("reason", out JsonElement reason) ? reason.GetString() ?? string.Empty : string.Empty,
            ReadVector3(element.GetProperty("groupPosition")),
            element.TryGetProperty("error", out JsonElement error) ? error.GetString() : null,
            layers)
        {
            Renderer = renderer,
        };
    }

    private static ProbeGpuInputs ReadGpu(JsonElement element) => new(
        ReadVector3(element.GetProperty("position")),
        ReadMatrix(element.GetProperty("left")),
        ReadMatrix(element.GetProperty("right")),
        ReadMatrix(element.GetProperty("leftB")),
        ReadMatrix(element.GetProperty("rightB")),
        ReadVector4(element.GetProperty("meshScale")),
        ReadVector4(element.GetProperty("centerPos")),
        ReadVector4(element.GetProperty("sideScale")),
        element.TryGetProperty("mainOutput", out JsonElement output) ? ReadMeshData(output) : null);

    private static ProbePiece ReadPiece(JsonElement element)
    {
        bool hasB = element.TryGetProperty("leftB", out JsonElement leftB);
        return new ProbePiece
        {
            Kind = element.GetProperty("kind").GetString() ?? string.Empty,
            Info = element.GetProperty("info").GetString() ?? string.Empty,
            MeshIndex = element.GetProperty("meshIndex").GetInt32(),
            Segment = element.GetProperty("segment").GetInt32(),
            TurnAround = element.GetProperty("turnAround").GetBoolean(),
            RequireHeightMap = element.GetProperty("requireHeightMap").GetBoolean(),
            SourceData = element.GetProperty("sourceData").GetInt32(),
            MainMesh = element.GetProperty("mainMesh").GetInt32(),
            Left = ReadMatrix(element.GetProperty("left")),
            Right = ReadMatrix(element.GetProperty("right")),
            LeftB = hasB ? ReadMatrix(leftB) : default,
            RightB = hasB ? ReadMatrix(element.GetProperty("rightB")) : default,
            CenterPos = element.TryGetProperty("centerPos", out JsonElement centerPos) ? ReadVector4(centerPos) : default,
            SideScale = element.TryGetProperty("sideScale", out JsonElement sideScale) ? ReadVector4(sideScale) : default,
            MeshScale = ReadVector4(element.GetProperty("meshScale")),
            Output = element.TryGetProperty("output", out JsonElement output) ? ReadMeshData(output) : null,
            MainOutput = element.TryGetProperty("mainOutput", out JsonElement mainOutput) ? ReadMeshData(mainOutput) : null,
            MainOutputNote = element.TryGetProperty("mainOutputNote", out JsonElement note) ? note.GetString() : null,
            Slot = element.TryGetProperty("slot", out JsonElement slot) ? slot.GetInt32() : -1,
            Gpu = element.TryGetProperty("gpu", out JsonElement gpu) ? ReadGpu(gpu) : null,
        };
    }

    private static ProbeMesh ReadMesh(JsonElement element)
    {
        string type = element.GetProperty("type").GetString() ?? string.Empty;
        bool readable = type == "meshData" || (element.TryGetProperty("readable", out JsonElement r) && r.GetBoolean());
        string name = element.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty;
        NetMeshData? data = readable ? ReadMeshData(element) : null;
        return new ProbeMesh(
            element.GetProperty("id").GetInt32(),
            element.GetProperty("role").GetString() ?? string.Empty,
            type,
            name,
            readable,
            element.GetProperty("vertexCount").GetInt32(),
            data);
    }

    /// <summary>Reads vertex arrays written as flat number arrays; Unity meshes keep their triangles per submesh.</summary>
    private static NetMeshData ReadMeshData(JsonElement element)
    {
        Vector3[] vertices = ReadVector3s(element, "vertices") ?? [];
        Vector3[]? normals = ReadVector3s(element, "normals");
        Vector4[]? tangents = ReadVector4s(element, "tangents");
        Vector2[]? uvs = ReadVector2s(element, "uvs");
        byte[]? colors = null;
        if (element.TryGetProperty("colors", out JsonElement colorsElement))
        {
            colors = new byte[colorsElement.GetArrayLength()];
            int i = 0;
            foreach (JsonElement value in colorsElement.EnumerateArray())
            {
                colors[i++] = value.GetByte();
            }
        }

        var triangles = new List<int>();
        if (element.TryGetProperty("triangles", out JsonElement trianglesElement))
        {
            AddInts(triangles, trianglesElement);
        }
        else if (element.TryGetProperty("submeshes", out JsonElement submeshes))
        {
            foreach (JsonElement submesh in submeshes.EnumerateArray())
            {
                AddInts(triangles, submesh.GetProperty("triangles"));
            }
        }

        int count = vertices.Length;
        return new NetMeshData(
            vertices,
            normals?.Length == count ? normals : null,
            tangents?.Length == count ? tangents : null,
            uvs?.Length == count ? uvs : null,
            colors?.Length == count * 4 ? colors : null,
            [.. triangles]);
    }

    private static void AddInts(List<int> target, JsonElement array)
    {
        foreach (JsonElement value in array.EnumerateArray())
        {
            target.Add(value.GetInt32());
        }
    }

    private static float[] ReadFloats(JsonElement array)
    {
        float[] values = new float[array.GetArrayLength()];
        int i = 0;
        foreach (JsonElement value in array.EnumerateArray())
        {
            values[i++] = value.GetSingle();
        }

        return values;
    }

    private static Vector2[]? ReadVector2s(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array))
        {
            return null;
        }

        float[] f = ReadFloats(array);
        var values = new Vector2[f.Length / 2];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new Vector2(f[i * 2], f[(i * 2) + 1]);
        }

        return values;
    }

    private static Vector3[]? ReadVector3s(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array))
        {
            return null;
        }

        float[] f = ReadFloats(array);
        var values = new Vector3[f.Length / 3];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new Vector3(f[i * 3], f[(i * 3) + 1], f[(i * 3) + 2]);
        }

        return values;
    }

    private static Vector4[]? ReadVector4s(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array))
        {
            return null;
        }

        float[] f = ReadFloats(array);
        var values = new Vector4[f.Length / 4];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new Vector4(f[i * 4], f[(i * 4) + 1], f[(i * 4) + 2], f[(i * 4) + 3]);
        }

        return values;
    }

    private static Vector3 ReadVector3(JsonElement array)
    {
        float[] f = ReadFloats(array);
        return new Vector3(f[0], f[1], f[2]);
    }

    private static Vector4 ReadVector4(JsonElement array)
    {
        float[] f = ReadFloats(array);
        return new Vector4(f[0], f[1], f[2], f[3]);
    }

    private static NetControlMatrix ReadMatrix(JsonElement array) => NetControlMatrix.FromRows(ReadFloats(array));
}

/// <summary>A probed segment or node.</summary>
/// <param name="Label">What the exporter chose it as, such as <c>junction-4way</c>.</param>
/// <param name="Kind"><c>segment</c> or <c>node</c>.</param>
/// <param name="Id">The game's id of the segment or node.</param>
/// <param name="Info">The prefab's name.</param>
/// <param name="Reason">Why it was chosen.</param>
/// <param name="GroupPosition">The point everything is relative to, in game coordinates.</param>
/// <param name="Error">The exporter's error, if probing it failed.</param>
/// <param name="Layers">The render layers it has meshes in.</param>
public sealed record ProbeObject(
    string Label,
    string Kind,
    int Id,
    string Info,
    string Reason,
    Vector3 GroupPosition,
    string? Error,
    IReadOnlyList<ProbeLayer> Layers)
{
    /// <summary>For nodes the game had drawn: its renderer's inputs, one per instance in the node's chain.</summary>
    public IReadOnlyList<ProbeGpuInputs> Renderer { get; init; } = [];
}

/// <summary>
/// The inputs of a junction piece's main mesh as the game's renderer computes them (the GPU path), relative
/// to <paramref name="Position"/>, where the mesh is drawn.
/// </summary>
/// <param name="Position">Where the renderer draws the mesh: the node's position.</param>
/// <param name="Left">The shader's <c>_LeftMatrix</c>.</param>
/// <param name="Right">The shader's <c>_RightMatrix</c>.</param>
/// <param name="LeftB">The shader's <c>_LeftMatrixB</c>.</param>
/// <param name="RightB">The shader's <c>_RightMatrixB</c>.</param>
/// <param name="MeshScale">The shader's <c>_MeshScale</c>.</param>
/// <param name="CenterPos">The shader's <c>_CenterPos</c>.</param>
/// <param name="SideScale">The shader's <c>_SideScale</c>.</param>
/// <param name="MainOutput">The main mesh bent with these inputs by the game's CPU function, if it was readable.</param>
public sealed record ProbeGpuInputs(
    Vector3 Position,
    NetControlMatrix Left,
    NetControlMatrix Right,
    NetControlMatrix LeftB,
    NetControlMatrix RightB,
    Vector4 MeshScale,
    Vector4 CenterPos,
    Vector4 SideScale,
    NetMeshData? MainOutput);

/// <summary>A render layer of a probed object.</summary>
/// <param name="Layer">The game's layer number.</param>
/// <param name="Game">The game's output for the whole object in this layer, if it has any.</param>
/// <param name="Pieces">The pieces the probe computed, in the game's order.</param>
public sealed record ProbeLayer(int Layer, NetMeshData? Game, IReadOnlyList<ProbePiece> Pieces);

/// <summary>One mesh the game bends for a probed object: the inputs of its bending function and the outputs.</summary>
public sealed record ProbePiece
{
    /// <summary><c>segment</c>, <c>junction</c>, <c>end</c>, <c>bendSegment</c> or <c>bendNode</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The prefab the mesh belongs to.</summary>
    public required string Info { get; init; }

    /// <summary>The mesh's index in the prefab.</summary>
    public required int MeshIndex { get; init; }

    /// <summary>The segment the piece belongs to.</summary>
    public required int Segment { get; init; }

    /// <summary>Whether the mesh is drawn turned around.</summary>
    public required bool TurnAround { get; init; }

    /// <summary>Whether the game takes the mesh's height from the terrain.</summary>
    public required bool RequireHeightMap { get; init; }

    /// <summary>The level-of-detail mesh data the game bent, as a mesh index.</summary>
    public required int SourceData { get; init; }

    /// <summary>The main mesh, as a mesh index, or −1.</summary>
    public required int MainMesh { get; init; }

    /// <summary>The first control matrix.</summary>
    public required NetControlMatrix Left { get; init; }

    /// <summary>The second control matrix.</summary>
    public required NetControlMatrix Right { get; init; }

    /// <summary>The third control matrix (junctions and dead ends).</summary>
    public NetControlMatrix LeftB { get; init; }

    /// <summary>The fourth control matrix (junctions and dead ends).</summary>
    public NetControlMatrix RightB { get; init; }

    /// <summary>The mesh scale.</summary>
    public required Vector4 MeshScale { get; init; }

    /// <summary>The centre position (junctions and dead ends).</summary>
    public Vector4 CenterPos { get; init; }

    /// <summary>The side scales (junctions and dead ends).</summary>
    public Vector4 SideScale { get; init; }

    /// <summary>The game's output for the level-of-detail mesh.</summary>
    public NetMeshData? Output { get; init; }

    /// <summary>The game's function applied to the main mesh, if it was readable.</summary>
    public NetMeshData? MainOutput { get; init; }

    /// <summary>Why there is no <see cref="MainOutput"/>.</summary>
    public string? MainOutputNote { get; init; }

    /// <summary>For junction pieces: the node's segment slot.</summary>
    public int Slot { get; init; } = -1;

    /// <summary>For junction pieces: the renderer's inputs for the main mesh.</summary>
    public ProbeGpuInputs? Gpu { get; init; }
}

/// <summary>A mesh in the probe file.</summary>
/// <param name="Id">Its index.</param>
/// <param name="Role">What it is for: <c>lodData</c>, <c>main</c>, <c>source</c> or <c>lod</c>.</param>
/// <param name="Type"><c>meshData</c> (the game's CPU copy) or <c>unityMesh</c>.</param>
/// <param name="Name">The Unity mesh's name.</param>
/// <param name="Readable">Whether its data could be read and is included.</param>
/// <param name="VertexCount">Its number of vertices.</param>
/// <param name="Data">Its data, if readable.</param>
public sealed record ProbeMesh(int Id, string Role, string Type, string Name, bool Readable, int VertexCount, NetMeshData? Data);
