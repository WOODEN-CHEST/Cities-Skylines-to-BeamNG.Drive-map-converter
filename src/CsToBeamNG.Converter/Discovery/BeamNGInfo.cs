namespace CsToBeamNG.Converter.Discovery;

/// <summary>Where BeamNG.drive and its user folder are, and which version is installed.</summary>
public sealed record BeamNGInfo
{
    /// <summary>The install folder, or <see langword="null"/> if not found. Never write into it.</summary>
    public FoundPath? InstallDir { get; init; }

    /// <summary>The Steam build id from the app manifest, if the game was found through Steam.</summary>
    public string? SteamBuildId { get; init; }

    /// <summary>The game version, such as <c>0.40.5.0</c>.</summary>
    public string? Version { get; init; }

    /// <summary>The game's own build number, such as <c>302427</c>.</summary>
    public string? EngineBuild { get; init; }

    /// <summary>The file the version was read from.</summary>
    public string? VersionSource { get; init; }

    /// <summary>The user folder (the <c>current</c> folder in the default layout), where levels are installed.</summary>
    public FoundPath? UserDir { get; init; }

    /// <summary>The folder for unpacked mods in the user folder, <c>mods/unpacked</c>.</summary>
    public FoundPath? UnpackedModsDir { get; init; }

    /// <summary>The game's log file, <c>beamng.log</c> in the user folder.</summary>
    public FoundPath? LogFile { get; init; }
}
