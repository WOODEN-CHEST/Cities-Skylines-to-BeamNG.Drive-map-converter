namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A lane of a network segment, with its fields as the game stores them in <c>NetLane</c> (docs/dump-format.md,
/// <c>net-lanes</c>; docs/research/cs1-networks.md, Lanes). Its properties, such as its type and speed limit, are those
/// of lane <see cref="Index"/> of the segment's prefab.
/// </summary>
public sealed class NetLaneRecord
{
    /// <summary>
    /// <see cref="Flags"/>: the game set the lane up for left-hand traffic (<c>NetLane.Flags.Inverted</c>); the game
    /// sets it on every lane of a city that drives on the left.
    /// </summary>
    public const ushort InvertedFlag = 0x4;

    /// <summary><see cref="Flags"/>: the lane's arrow allows going straight on at its end (<c>Forward</c>).</summary>
    public const ushort ForwardFlag = 0x10;

    /// <summary><see cref="Flags"/>: the lane's arrow allows turning left at its end (<c>Left</c>).</summary>
    public const ushort LeftFlag = 0x20;

    /// <summary><see cref="Flags"/>: the lane's arrow allows turning right at its end (<c>Right</c>).</summary>
    public const ushort RightFlag = 0x40;

    /// <summary>The lane's id: its index in the game's lane buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The id of the segment the lane belongs to.</summary>
    public uint Segment { get; set; }

    /// <summary>The lane's index among the lanes of the segment's prefab, which the game keeps in the same order.</summary>
    public ushort Index { get; set; }

    /// <summary>The lane's flags: the raw value of <c>NetLane.m_flags</c> (<c>NetLane.Flags</c>).</summary>
    public ushort Flags { get; set; }

    /// <summary>
    /// The lane's curve. It always runs from the segment's start node towards its end node; the lane's direction of
    /// travel comes from the prefab's lane and the segment's flags.
    /// </summary>
    public DumpBezier3 Bezier { get; set; }

    /// <summary>The lane's length as the game computes it (<c>m_length</c>): the length of the curve's control polygon, in metres.</summary>
    public float Length { get; set; }
}
