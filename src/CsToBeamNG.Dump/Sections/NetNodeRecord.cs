namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A network node: an end, bend or junction of roads, tracks, paths, pipes and the game's other networks, with its
/// fields as the game stores them in <c>NetNode</c> (docs/dump-format.md, <c>net-nodes</c>; docs/research/cs1-networks.md,
/// Data model). The flag constants are the bits of <see cref="Flags"/> that the converter uses.
/// </summary>
public sealed class NetNodeRecord
{
    /// <summary>The number of segment slots a node has.</summary>
    public const int SegmentSlots = 8;

    /// <summary><see cref="Flags"/>: the node exists (<c>NetNode.Flags.Created</c>).</summary>
    public const uint CreatedFlag = 0x1;

    /// <summary><see cref="Flags"/>: a dead end, with one segment (<c>End</c>).</summary>
    public const uint EndFlag = 0x10;

    /// <summary><see cref="Flags"/>: a middle node, where one segment continues into the next of the same kind (<c>Middle</c>).</summary>
    public const uint MiddleFlag = 0x20;

    /// <summary><see cref="Flags"/>: a bend between two segments that the game draws with a bend piece (<c>Bend</c>).</summary>
    public const uint BendFlag = 0x40;

    /// <summary><see cref="Flags"/>: a junction (<c>Junction</c>).</summary>
    public const uint JunctionFlag = 0x80;

    /// <summary><see cref="Flags"/>: an outside connection at the map's edge, where traffic enters and leaves the map (<c>Outside</c>).</summary>
    public const uint OutsideFlag = 0x400;

    /// <summary><see cref="Flags"/>: the node lies on the ground (<c>OnGround</c>).</summary>
    public const uint OnGroundFlag = 0x4000;

    /// <summary><see cref="Flags"/>: the node lies underground, in a tunnel (<c>Underground</c>).</summary>
    public const uint UndergroundFlag = 0x80000;

    /// <summary><see cref="Flags"/>: a transition, such as a tunnel portal together with <see cref="UndergroundFlag"/> (<c>Transition</c>).</summary>
    public const uint TransitionFlag = 0x100000;

    /// <summary><see cref="Flags"/>: a level crossing of a road and a track (<c>LevelCrossing</c>).</summary>
    public const uint LevelCrossingFlag = 0x200000;

    /// <summary><see cref="Flags"/>: the junction has traffic lights (<c>TrafficLights</c>).</summary>
    public const uint TrafficLightsFlag = 0x800000;

    /// <summary>
    /// <see cref="Flags"/>: the player switched the junction's traffic lights on or off, against the game's default
    /// (<c>CustomTrafficLights</c>); <see cref="TrafficLightsFlag"/> says which.
    /// </summary>
    public const uint CustomTrafficLightsFlag = 0x80000000;

    /// <summary>The node's id: its index in the game's node buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The node's flags: the raw value of <c>NetNode.m_flags</c> (<c>NetNode.Flags</c>).</summary>
    public uint Flags { get; set; }

    /// <summary>The node's second flags: the raw value of <c>NetNode.m_flags2</c> (<c>NetNode.Flags2</c>).</summary>
    public uint Flags2 { get; set; }

    /// <summary>The index of the node's network prefab in the game's prefab collection (<c>m_infoIndex</c>), described in <c>net-prefabs</c>.</summary>
    public uint Prefab { get; set; }

    /// <summary>The node's position, in metres.</summary>
    public DumpVector3 Position { get; set; }

    /// <summary>The id of the node's building, such as a bridge pillar or a pylon; 0 for none.</summary>
    public uint Building { get; set; }

    /// <summary>The ids of the connected segments, <see cref="SegmentSlots"/> slots as the game keeps them; 0 is an empty slot.</summary>
    public uint[] Segments { get; } = new uint[SegmentSlots];

    /// <summary>The node's elevation as the game stores it (<c>m_elevation</c>): for elevated and underground networks, the height above or depth below the ground.</summary>
    public byte Elevation { get; set; }

    /// <summary>The node's height offset in 1/64 m (<c>m_heightOffset</c>), which the game adds to the corners at this node.</summary>
    public byte HeightOffset { get; set; }

    /// <summary>Returns whether all the given bits of <see cref="Flags"/> are set.</summary>
    /// <param name="flags">One or more of the flag constants.</param>
    public bool HasFlags(uint flags) => (Flags & flags) == flags;
}
