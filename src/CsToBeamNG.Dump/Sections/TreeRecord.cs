namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A tree (<c>TreeInstance</c>), with its position through the game's accessor and the scale and brightness the game
/// derives from its id (docs/dump-format.md, <c>trees</c>; docs/research/cs1-assets.md, Placement). The game doesn't
/// turn trees. The flag constants are the bits of <see cref="Flags"/> that the converter uses.
/// </summary>
public sealed class TreeRecord
{
    /// <summary><see cref="Flags"/>: the tree exists (<c>TreeInstance.Flags.Created</c>).</summary>
    public const ushort CreatedFlag = 0x1;

    /// <summary><see cref="Flags"/>: the tree is hidden (<c>Hidden</c>); the game doesn't draw it.</summary>
    public const ushort HiddenFlag = 0x4;

    /// <summary><see cref="Flags"/>: the tree keeps its height; otherwise the game puts it on the ground when the terrain changes (<c>FixedHeight</c>).</summary>
    public const ushort FixedHeightFlag = 0x20;

    /// <summary><see cref="Flags"/>: the tree was damaged by fire (<c>FireDamage</c>).</summary>
    public const ushort FireDamageFlag = 0x40;

    /// <summary><see cref="Flags"/>: the tree is burning (<c>Burning</c>).</summary>
    public const ushort BurningFlag = 0x80;

    /// <summary><see cref="Flags"/>: the bits of the tree's grow state, 0 to 15 (<c>GrowState</c>); the game doesn't draw a tree whose grow state is 0.</summary>
    public const ushort GrowStateMask = 0xF00;

    /// <summary>The tree's id: its index in the game's tree buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The index of the tree's prefab in the game's prefab collection (<c>m_infoIndex</c>), described in <c>tree-prefabs</c>.</summary>
    public uint Prefab { get; set; }

    /// <summary>The tree's position from the game's accessor (<c>TreeInstance.Position</c>), in metres.</summary>
    public DumpVector3 Position { get; set; }

    /// <summary>The scale the game draws the tree with, derived from its id as the renderer does it.</summary>
    public float Scale { get; set; }

    /// <summary>
    /// The brightness the game draws the tree with, derived from its id after the scale; the game multiplies the
    /// prefab's default colour by it.
    /// </summary>
    public float Brightness { get; set; }

    /// <summary>The tree's flags: the raw value of <c>TreeInstance.m_flags</c> (<c>TreeInstance.Flags</c>).</summary>
    public ushort Flags { get; set; }

    /// <summary>The tree's grow state, 0 to 15: the bits <see cref="GrowStateMask"/> of <see cref="Flags"/>.</summary>
    public int GrowState => (Flags & GrowStateMask) >> 8;

    /// <summary>Returns whether all the given bits of <see cref="Flags"/> are set.</summary>
    /// <param name="flags">One or more of the flag constants.</param>
    public bool HasFlags(ushort flags) => (Flags & flags) == flags;

    /// <summary>Whether the game draws the tree: its grow state isn't 0 and it isn't hidden (the check in <c>TreeInstance.RenderInstance</c>).</summary>
    public bool IsDrawn => GrowState != 0 && (Flags & HiddenFlag) == 0;
}
