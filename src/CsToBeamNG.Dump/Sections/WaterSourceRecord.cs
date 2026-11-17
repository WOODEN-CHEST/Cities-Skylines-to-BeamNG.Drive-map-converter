namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A water source of the game's water simulation, with its fields as the game stores them (docs/dump-format.md,
/// <c>water</c>; docs/research/cs1-save-and-world.md, Water).
/// </summary>
public sealed class WaterSourceRecord
{
    /// <summary>The <see cref="Type"/> of a source placed with the map editor, which fills its area up to <see cref="Target"/>.</summary>
    public const ushort NaturalType = 1;

    /// <summary>The <see cref="Type"/> of a building's source, such as a water pumping station's intake and a sewage outlet.</summary>
    public const ushort FacilityType = 2;

    /// <summary>The <see cref="Type"/> of a water treatment building's source.</summary>
    public const ushort CleanerType = 3;

    /// <summary>The game's id of the source: its index in the game's list of sources, plus 1.</summary>
    public uint Id { get; set; }

    /// <summary>The kind of source: <see cref="NaturalType"/>, <see cref="FacilityType"/> or <see cref="CleanerType"/>.</summary>
    public ushort Type { get; set; }

    /// <summary>The water surface height the source works towards, in 1/64 m (<see cref="WaterSection.MetersPerHeightUnit"/>).</summary>
    public ushort Target { get; set; }

    /// <summary>The X of the point where the source takes water in, in metres.</summary>
    public float InputX { get; set; }

    /// <summary>The Y (height) of the point where the source takes water in, in metres.</summary>
    public float InputY { get; set; }

    /// <summary>The Z of the point where the source takes water in, in metres.</summary>
    public float InputZ { get; set; }

    /// <summary>The X of the point where the source lets water out, in metres.</summary>
    public float OutputX { get; set; }

    /// <summary>The Y (height) of the point where the source lets water out, in metres.</summary>
    public float OutputY { get; set; }

    /// <summary>The Z of the point where the source lets water out, in metres.</summary>
    public float OutputZ { get; set; }

    /// <summary>The water the source holds, in the game's units.</summary>
    public uint Water { get; set; }

    /// <summary>The pollution the source holds, in the game's units.</summary>
    public uint Pollution { get; set; }

    /// <summary>How fast the source takes water in, in the game's units.</summary>
    public uint InputRate { get; set; }

    /// <summary>How fast the source lets water out, in the game's units.</summary>
    public uint OutputRate { get; set; }

    /// <summary>The water the source let out in the simulation's last step, in the game's units.</summary>
    public uint Flow { get; set; }
}
