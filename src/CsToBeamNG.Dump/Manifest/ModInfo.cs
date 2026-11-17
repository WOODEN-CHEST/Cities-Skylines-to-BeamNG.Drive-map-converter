namespace CsToBeamNG.Dump.Manifest;

/// <summary>A mod that was enabled during the export.</summary>
public sealed class ModInfo
{
    /// <summary>Creates an empty instance, to fill in.</summary>
    public ModInfo()
    {
    }

    /// <summary>Creates an instance.</summary>
    /// <param name="name">The mod's name.</param>
    /// <param name="workshopId">The Steam Workshop item id, or <see langword="null"/> for a local mod.</param>
    public ModInfo(string name, string? workshopId)
    {
        Name = name;
        WorkshopId = workshopId;
    }

    /// <summary>The mod's name as the game shows it. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The Steam Workshop item id, in decimal digits, or <see langword="null"/> for a mod that isn't from
    /// the workshop. A string, because the ids are 64-bit numbers.
    /// </summary>
    public string? WorkshopId { get; set; }
}
