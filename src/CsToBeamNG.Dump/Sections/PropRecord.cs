namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A prop placed on its own (<c>PropInstance</c>), with its position and angle through the game's accessors and the
/// scale and colour the game derives from its id (docs/dump-format.md, <c>props</c>; docs/research/cs1-assets.md,
/// Placement). The flag constants are the bits of <see cref="Flags"/> that the converter uses.
/// </summary>
public sealed class PropRecord
{
    /// <summary><see cref="Flags"/>: the prop exists (<c>PropInstance.Flags.Created</c>).</summary>
    public const ushort CreatedFlag = 0x1;

    /// <summary><see cref="Flags"/>: the prop was deleted (<c>Deleted</c>).</summary>
    public const ushort DeletedFlag = 0x2;

    /// <summary><see cref="Flags"/>: the prop is hidden (<c>Hidden</c>); the game doesn't draw it.</summary>
    public const ushort HiddenFlag = 0x4;

    /// <summary><see cref="Flags"/>: the prop keeps its height; otherwise the game puts it on the ground when the terrain changes (<c>FixedHeight</c>).</summary>
    public const ushort FixedHeightFlag = 0x20;

    /// <summary><see cref="Flags"/>: something else stands in the prop's place (<c>Blocked</c>); the game doesn't draw it.</summary>
    public const ushort BlockedFlag = 0x40;

    /// <summary>The prop's id: its index in the game's prop buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The index of the prop's prefab in the game's prefab collection (<c>m_infoIndex</c>), described in <c>prop-prefabs</c>.</summary>
    public uint Prefab { get; set; }

    /// <summary>The prop's position from the game's accessor (<c>PropInstance.Position</c>), in metres.</summary>
    public DumpVector3 Position { get; set; }

    /// <summary>The prop's angle from the game's accessor (<c>PropInstance.Angle</c>), in radians, about the downward axis.</summary>
    public float Angle { get; set; }

    /// <summary>The scale the game draws the prop with, derived from its id as the renderer does it.</summary>
    public float Scale { get; set; }

    /// <summary>The colour the game draws the prop with: the prefab's <c>GetColor</c> after the scale, from the same randomizer.</summary>
    public DumpColor Color { get; set; }

    /// <summary>The prop's flags: the raw value of <c>PropInstance.m_flags</c> (<c>PropInstance.Flags</c>).</summary>
    public ushort Flags { get; set; }

    /// <summary>Returns whether all the given bits of <see cref="Flags"/> are set.</summary>
    /// <param name="flags">One or more of the flag constants.</param>
    public bool HasFlags(ushort flags) => (Flags & flags) == flags;

    /// <summary>Whether the game draws the prop: neither hidden nor blocked (the check in <c>PropInstance.RenderInstance</c>).</summary>
    public bool IsDrawn => (Flags & (HiddenFlag | BlockedFlag)) == 0;
}
