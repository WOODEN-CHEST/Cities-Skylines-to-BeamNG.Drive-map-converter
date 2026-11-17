namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A sub-building of a building prefab (<c>BuildingInfo.SubInfo</c>): a building the game creates with the main one,
/// placed relative to it (docs/dump-format.md, <c>building-prefabs</c>). The game keeps sub-buildings as buildings of
/// their own, linked to the main one through <see cref="BuildingRecord.ParentBuilding"/>.
/// </summary>
public sealed class BuildingSubBuilding
{
    /// <summary>The index of the sub-building's prefab in the game's prefab collection; <see langword="null"/> if it isn't loaded.</summary>
    public uint? Prefab { get; set; }

    /// <summary>The sub-building's prefab name; empty if it isn't loaded.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its position relative to the main building (<c>m_position</c>), in metres.</summary>
    public DumpVector3 Position { get; set; }

    /// <summary>Its angle relative to the main building (<c>m_angle</c>), in degrees, as the game stores it.</summary>
    public float AngleDegrees { get; set; }

    /// <summary>Whether it keeps its height relative to the main building (<c>m_fixedHeight</c>).</summary>
    public bool FixedHeight { get; set; }
}
