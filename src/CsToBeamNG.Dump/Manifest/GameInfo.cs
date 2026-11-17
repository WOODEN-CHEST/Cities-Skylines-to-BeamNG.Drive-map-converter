using System.Collections.Generic;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>The game a dump was exported from.</summary>
public sealed class GameInfo
{
    /// <summary>The game's version as the game reports it. Required.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The mods that were enabled during the export.</summary>
    public IList<ModInfo> Mods { get; } = new List<ModInfo>();
}
