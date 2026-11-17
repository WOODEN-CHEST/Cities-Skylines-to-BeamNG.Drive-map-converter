namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A network segment between two nodes, with its fields as the game stores them in <c>NetSegment</c> and the geometry
/// the game computes for it: the centre curve and the four corners (docs/dump-format.md, <c>net-segments</c>;
/// docs/research/cs1-networks.md, Data model and Corners).
/// </summary>
/// <remarks>
/// A corner's side is seen from its own node, looking into the segment, as the game's <c>leftSide</c> parameter means it.
/// So <see cref="StartLeft"/> and <see cref="EndRight"/> lie on the same side of the segment: its left side when going
/// from the start node to the end node.
/// </remarks>
public sealed class NetSegmentRecord
{
    /// <summary>
    /// <see cref="Flags"/>: the segment is inverted (<c>NetSegment.Flags.Invert</c>). Its lanes are mirrored across the
    /// centre line, and their directions are reversed relative to the segment.
    /// </summary>
    public const uint InvertFlag = 0x10;

    /// <summary>The segment's id: its index in the game's segment buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The segment's flags: the raw value of <c>NetSegment.m_flags</c> (<c>NetSegment.Flags</c>).</summary>
    public uint Flags { get; set; }

    /// <summary>The segment's second flags: the raw value of <c>NetSegment.m_flags2</c> (<c>NetSegment.Flags2</c>).</summary>
    public uint Flags2 { get; set; }

    /// <summary>The index of the segment's network prefab in the game's prefab collection (<c>m_infoIndex</c>), described in <c>net-prefabs</c>.</summary>
    public uint Prefab { get; set; }

    /// <summary>The id of the start node.</summary>
    public uint StartNode { get; set; }

    /// <summary>The id of the end node.</summary>
    public uint EndNode { get; set; }

    /// <summary>The centre line's direction at the start node, pointing into the segment (<c>m_startDirection</c>).</summary>
    public DumpVector3 StartDirection { get; set; }

    /// <summary>The centre line's direction at the end node, pointing into the segment (<c>m_endDirection</c>).</summary>
    public DumpVector3 EndDirection { get; set; }

    /// <summary>The middle of the segment's surface (<c>m_middlePosition</c>), in metres.</summary>
    public DumpVector3 MiddlePosition { get; set; }

    /// <summary>The centre line from the start node to the end node, as the game computes it.</summary>
    public DumpBezier3 Bezier { get; set; }

    /// <summary>The corner at the start node on its left side.</summary>
    public NetCorner StartLeft { get; set; }

    /// <summary>The corner at the start node on its right side.</summary>
    public NetCorner StartRight { get; set; }

    /// <summary>The corner at the end node on its left side, seen from the end node.</summary>
    public NetCorner EndLeft { get; set; }

    /// <summary>The corner at the end node on its right side, seen from the end node.</summary>
    public NetCorner EndRight { get; set; }

    /// <summary>The game's average length of the segment's lanes (<c>m_averageLength</c>), in metres.</summary>
    public float AverageLength { get; set; }

    /// <summary>Returns whether all the given bits of <see cref="Flags"/> are set.</summary>
    /// <param name="flags">One or more of the flag constants.</param>
    public bool HasFlags(uint flags) => (Flags & flags) == flags;
}
