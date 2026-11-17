using System;
using System.CommandLine;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Cli;

/// <summary>
/// The command-line options that override discovered locations. Each one takes precedence over
/// the environment variable of the same meaning.
/// </summary>
internal sealed class LocationOptions
{
    private readonly Option<string?> _steamDir = Create(
        "--steam-dir", "Steam folder (the one that contains steamapps).", DiscoveryOverrides.SteamDirVariable);

    private readonly Option<string?> _citiesSkylinesDir = Create(
        "--cs1-dir", "Cities: Skylines install folder.", DiscoveryOverrides.CitiesSkylinesDirVariable);

    private readonly Option<string?> _citiesSkylinesUserDir = Create(
        "--cs1-user-dir", "Cities: Skylines user data folder.", DiscoveryOverrides.CitiesSkylinesUserDirVariable);

    private readonly Option<string?> _beamNGDir = Create(
        "--beamng-dir", "BeamNG.drive install folder.", DiscoveryOverrides.BeamNGDirVariable);

    private readonly Option<string?> _beamNGUserDir = Create(
        "--beamng-user-dir", "BeamNG.drive user folder (normally the one named current).", DiscoveryOverrides.BeamNGUserDirVariable);

    private readonly Option<string?>[] _offered;

    /// <summary>Offers every location option.</summary>
    public LocationOptions()
        : this(beamNGUserDirOnly: false)
    {
    }

    private LocationOptions(bool beamNGUserDirOnly)
    {
        _offered = beamNGUserDirOnly ? [_beamNGUserDir] : [_steamDir, _citiesSkylinesDir, _citiesSkylinesUserDir, _beamNGDir, _beamNGUserDir];
    }

    /// <summary>Offers only <c>--beamng-user-dir</c>, for commands that only write into BeamNG.drive's user folder.</summary>
    public static LocationOptions ForBeamNGUserDir() => new(beamNGUserDirOnly: true);

    public void AddTo(Command command)
    {
        foreach (Option<string?> option in _offered)
        {
            command.Options.Add(option);
        }
    }

    public DiscoveryOverrides GetOverrides(ParseResult parseResult) => new()
    {
        SteamDir = Read(parseResult, _steamDir),
        CitiesSkylinesDir = Read(parseResult, _citiesSkylinesDir),
        CitiesSkylinesUserDir = Read(parseResult, _citiesSkylinesUserDir),
        BeamNGDir = Read(parseResult, _beamNGDir),
        BeamNGUserDir = Read(parseResult, _beamNGUserDir),
    };

    private static Option<string?> Create(string name, string description, string environmentVariable) => new(name)
    {
        Description = $"{description} Overrides {environmentVariable}.",
        HelpName = "dir",
    };

    private PathOverride? Read(ParseResult parseResult, Option<string?> option) =>
        Array.IndexOf(_offered, option) >= 0 && parseResult.GetValue(option) is { Length: > 0 } value
            ? new PathOverride(value, option.Name)
            : null;
}
