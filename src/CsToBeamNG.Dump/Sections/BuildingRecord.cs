namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A building, sub-buildings included, with its fields as the game stores them in <c>Building</c> and the values the
/// game derives for it: where it draws the mesh and in which colour (docs/dump-format.md, <c>buildings</c>;
/// docs/research/cs1-assets.md, Placement). The flag constants are the bits of <see cref="Flags"/> that the converter uses.
/// </summary>
/// <remarks>
/// The lot is <see cref="Width"/> × <see cref="Length"/> cells of 8 m, centred on <see cref="Position"/> and turned by
/// <see cref="Angle"/>; its front, which faces the road, lies towards (−sin angle, 0, cos angle).
/// </remarks>
public sealed class BuildingRecord
{
    /// <summary>The size of a lot cell, in metres.</summary>
    public const float CellSizeMeters = 8;

    /// <summary><see cref="Flags"/>: the building exists (<c>Building.Flags.Created</c>).</summary>
    public const uint CreatedFlag = 0x1;

    /// <summary><see cref="Flags"/>: the building was deleted (<c>Deleted</c>); the game doesn't draw it.</summary>
    public const uint DeletedFlag = 0x2;

    /// <summary><see cref="Flags"/>: placed with the map editor, such as a rock formation (<c>Original</c>).</summary>
    public const uint OriginalFlag = 0x4;

    /// <summary><see cref="Flags"/>: the player can't select it, as with sub-buildings (<c>Untouchable</c>).</summary>
    public const uint UntouchableFlag = 0x10;

    /// <summary><see cref="Flags"/>: its construction is finished (<c>Completed</c>).</summary>
    public const uint CompletedFlag = 0x10000;

    /// <summary><see cref="Flags"/>: the building is abandoned (<c>Abandoned</c>).</summary>
    public const uint AbandonedFlag = 0x40000;

    /// <summary><see cref="Flags"/>: the building collapsed or burned down (<c>Collapsed</c>, the same bit as <c>BurnedDown</c>).</summary>
    public const uint CollapsedFlag = 0x400000;

    /// <summary><see cref="Flags"/>: the building is hidden (<c>Hidden</c>); the game doesn't draw it.</summary>
    public const uint HiddenFlag = 0x8000000;

    /// <summary>The building's id: its index in the game's building buffer.</summary>
    public uint Id { get; set; }

    /// <summary>The building's flags: the raw value of <c>Building.m_flags</c> (<c>Building.Flags</c>).</summary>
    public uint Flags { get; set; }

    /// <summary>The building's second flags: the raw value of <c>Building.m_flags2</c> (<c>Building.Flags2</c>).</summary>
    public uint Flags2 { get; set; }

    /// <summary>The index of the building's prefab in the game's prefab collection (<c>m_infoIndex</c>), described in <c>building-prefabs</c>.</summary>
    public uint Prefab { get; set; }

    /// <summary>The centre of the building's lot (<c>m_position</c>), in metres.</summary>
    public DumpVector3 Position { get; set; }

    /// <summary>The building's angle (<c>m_angle</c>), in radians: the game turns it by this angle about the downward axis.</summary>
    public float Angle { get; set; }

    /// <summary>
    /// Where the game draws the building's mesh: <c>Building.CalculateMeshPosition()</c>, which moves it from the lot's
    /// centre along the lot when the lot is deeper than the prefab's cells.
    /// </summary>
    public DumpVector3 MeshPosition { get; set; }

    /// <summary>The id of the building this one belongs to as a sub-building (<c>m_parentBuilding</c>); 0 for none.</summary>
    public uint ParentBuilding { get; set; }

    /// <summary>The id of the building's first sub-building, or of its parent's next one (<c>m_subBuilding</c>); 0 for none.</summary>
    public uint SubBuilding { get; set; }

    /// <summary>
    /// The building's colour as the game draws it in the normal view: what its AI's <c>GetColor</c> returns for no
    /// info mode, which the game writes into its building colour map. Usually the prefab's colour variation picked by
    /// the building's id; the game sets alpha to 1 for buildings with electricity and to 0 otherwise.
    /// </summary>
    public DumpColor Color { get; set; }

    /// <summary>The lot's width in cells (<c>m_width</c>), across the front.</summary>
    public byte Width { get; set; }

    /// <summary>The lot's length in cells (<c>m_length</c>), from the front to the back.</summary>
    public byte Length { get; set; }

    /// <summary>The building's level (<c>m_level</c>), as the game stores it.</summary>
    public byte Level { get; set; }

    /// <summary>How far the game extends the building's base downwards on a slope (<c>m_baseHeight</c>), in metres.</summary>
    public byte BaseHeight { get; set; }

    /// <summary>Returns whether all the given bits of <see cref="Flags"/> are set.</summary>
    /// <param name="flags">One or more of the flag constants.</param>
    public bool HasFlags(uint flags) => (Flags & flags) == flags;

    /// <summary>
    /// Whether the game draws the building: created, neither deleted nor hidden (the check in
    /// <c>Building.RenderInstance</c>).
    /// </summary>
    public bool IsDrawn => (Flags & (CreatedFlag | DeletedFlag | HiddenFlag)) == CreatedFlag;
}
