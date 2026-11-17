using UnityEngine;

namespace CsToBeamNG.Exporter.Probes;

/// <summary>Which of the game's static bending functions a <see cref="GroupPiece"/> goes through.</summary>
internal enum GroupPieceKind
{
    /// <summary>A segment mesh along a segment: <c>NetSegment.PopulateGroupData</c> (two control matrices).</summary>
    Segment,

    /// <summary>A segment's part of a junction: <c>NetNode.PopulateGroupData</c> with four control matrices.</summary>
    Junction,

    /// <summary>The cap of a dead end: <c>NetNode.PopulateGroupData</c> with four control matrices.</summary>
    End,

    /// <summary>A segment mesh drawn across a bend node: <c>NetSegment.PopulateGroupData</c>.</summary>
    BendSegment,

    /// <summary>A direct-connect node mesh drawn across a bend node: <c>NetNode.PopulateGroupData</c> with two control matrices.</summary>
    BendNode,
}

/// <summary>
/// One mesh that the game bends into a render group for a segment or node, with the inputs of the game's
/// bending function and its output. The probe computes the inputs the way the game's
/// <c>PopulateGroupData</c> methods do (docs/research/cs1-networks.md, Bending network meshes).
/// </summary>
internal sealed class GroupPiece
{
    public GroupPiece(GroupPieceKind kind, NetInfo info, int meshIndex)
    {
        Kind = kind;
        Info = info;
        MeshIndex = meshIndex;
    }

    public GroupPieceKind Kind { get; }

    /// <summary>The prefab passed to the bending function as <c>info</c>.</summary>
    public NetInfo Info { get; }

    /// <summary>The index of the mesh in <see cref="Info"/>'s <c>m_segments</c> or <c>m_nodes</c>.</summary>
    public int MeshIndex { get; }

    /// <summary>The segment mesh, for <see cref="GroupPieceKind.Segment"/> and <see cref="GroupPieceKind.BendSegment"/>.</summary>
    public NetInfo.Segment? SegmentMesh { get; set; }

    /// <summary>The node mesh, for the other kinds.</summary>
    public NetInfo.Node? NodeMesh { get; set; }

    /// <summary>The segment the piece belongs to: its own, a junction piece's, or a bend's first segment.</summary>
    public ushort SegmentId { get; set; }

    public bool TurnAround { get; set; }

    public Matrix4x4 Left { get; set; }

    public Matrix4x4 Right { get; set; }

    public Matrix4x4 LeftB { get; set; }

    public Matrix4x4 RightB { get; set; }

    public Vector4 MeshScale { get; set; }

    public Vector4 CenterPos { get; set; }

    public Vector4 SideScale { get; set; }

    public Vector4 ObjectIndex { get; set; }

    public Vector2 ColorIndex { get; set; }

    /// <summary>The game's output for this piece alone, from its static bending function.</summary>
    public RenderGroup.MeshData? Output { get; set; }

    public int OutputVertexCount { get; set; }

    public int OutputTriangleCount { get; set; }

    /// <summary>
    /// The game's bending function applied to the main mesh instead of the level-of-detail mesh, when the
    /// main mesh is readable. The game itself bends main meshes on the GPU.
    /// </summary>
    public RenderGroup.MeshData? MainOutput { get; set; }

    public int MainOutputVertexCount { get; set; }

    public int MainOutputTriangleCount { get; set; }

    /// <summary>Why <see cref="MainOutput"/> is missing, if it is.</summary>
    public string? MainOutputNote { get; set; }

    /// <summary>The mesh the game draws up close: <c>m_segmentMesh</c> or <c>m_nodeMesh</c>.</summary>
    public Mesh? MainMesh => SegmentMesh is not null ? SegmentMesh.m_segmentMesh : NodeMesh?.m_nodeMesh;

    /// <summary>For junction pieces: the node's segment slot (0–7) the piece belongs to.</summary>
    public int Slot { get; set; } = -1;

    /// <summary>
    /// For junction pieces: the inputs the renderer computes for the main mesh (the GPU path), which differ
    /// from the level-of-detail inputs, and the main mesh bent with them.
    /// </summary>
    public GpuParameters? Gpu { get; set; }

    /// <summary>The probe's mesh number of <see cref="SourceData"/>, or -1.</summary>
    public int SourceDataId { get; set; } = -1;

    /// <summary>The probe's mesh number of the mesh the game draws up close (<c>m_segmentMesh</c> or <c>m_nodeMesh</c>), or -1.</summary>
    public int MainMeshId { get; set; } = -1;

    /// <summary>The probe's mesh number of the prefab's own mesh (<c>m_mesh</c>), or -1.</summary>
    public int SourceMeshId { get; set; } = -1;

    /// <summary>The probe's mesh number of the prefab's level-of-detail mesh (<c>m_lodMesh</c>), or -1.</summary>
    public int LodMeshId { get; set; } = -1;

    /// <summary>Whether the segment mesh samples the terrain height for its vertices (then the probe's output depends on the terrain).</summary>
    public bool RequiresHeightMap => SegmentMesh is not null && SegmentMesh.m_requireHeightMap;

    /// <summary>The CPU copy of the mesh the bending function reads: the prefab's combined level-of-detail mesh data.</summary>
    public RenderGroup.MeshData? SourceData =>
        SegmentMesh?.m_combinedLod?.m_key.m_mesh?.m_data ?? NodeMesh?.m_combinedLod?.m_key.m_mesh?.m_data;
}

/// <summary>
/// The inputs the game's renderer computes for a junction piece's main mesh (<c>NetNode.RefreshJunctionData</c>),
/// relative to <see cref="Position"/>, where the renderer draws the mesh.
/// </summary>
internal sealed class GpuParameters
{
    public Vector3 Position { get; set; }

    public Matrix4x4 Left { get; set; }

    public Matrix4x4 Right { get; set; }

    public Matrix4x4 LeftB { get; set; }

    public Matrix4x4 RightB { get; set; }

    public Vector4 MeshScale { get; set; }

    public Vector4 CenterPos { get; set; }

    public Vector4 SideScale { get; set; }

    /// <summary>The main mesh bent by the game's CPU function with these inputs.</summary>
    public RenderGroup.MeshData? MainOutput { get; set; }

    public int MainOutputVertexCount { get; set; }

    public int MainOutputTriangleCount { get; set; }

    /// <summary>Reads the renderer's instance data of a junction piece: the fields its shader inputs come from.</summary>
    public static GpuParameters FromInstance(RenderManager.Instance data) => new()
    {
        Position = data.m_position,
        Left = data.m_dataMatrix0,
        Right = data.m_extraData.m_dataMatrix2,
        LeftB = data.m_extraData.m_dataMatrix3,
        RightB = data.m_dataMatrix1,
        MeshScale = data.m_dataVector0,
        CenterPos = data.m_dataVector1,
        SideScale = data.m_dataVector2,
    };
}
