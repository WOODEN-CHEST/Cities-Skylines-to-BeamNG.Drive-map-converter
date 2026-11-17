namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A prefab's item class (<c>ItemClass</c>), which says what kind of object it is: its name, its service, sub-service
/// and level as the game names them, and its layers (docs/dump-format.md, <c>building-prefabs</c>). The JSON files of the
/// prefab sections write it as the object <c>class</c>.
/// </summary>
public sealed class DumpItemClass
{
    /// <summary>
    /// <see cref="Layer"/>: the default layer, which the game shows in its normal view (<c>ItemClass.Layer.Default</c>).
    /// Objects on other layers, such as water pipes (0x2) and markers (0x40), show only in info views or editors.
    /// </summary>
    public const uint DefaultLayer = 0x1;

    /// <summary>The item class's name, such as <c>Low Residential - Level1</c>; empty if the prefab has no class.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The service, as the game names it (<c>ItemClass.Service</c>), such as <c>Residential</c>.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>The sub-service, as the game names it (<c>ItemClass.SubService</c>), such as <c>ResidentialLow</c>.</summary>
    public string SubService { get; set; } = string.Empty;

    /// <summary>The level, as the game names it (<c>ItemClass.Level</c>), such as <c>Level1</c>.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>The layers, the raw value of <c>ItemClass.Layer</c>, a set of bits.</summary>
    public uint Layer { get; set; }
}
