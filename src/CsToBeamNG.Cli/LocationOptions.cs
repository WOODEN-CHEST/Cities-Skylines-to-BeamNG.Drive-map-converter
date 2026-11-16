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

    public void AddTo(Command command)
    {
        command.Options.Add(_steamDir);
        command.Options.Add(_citiesSkylinesDir);
        command.Options.Add(_citiesSkylinesUserDir);
        command.Options.Add(_beamNGDir);
        command.Options.Add(_beamNGUserDir);
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

    private static PathOverride? Read(ParseResult parseResult, Option<string?> option) =>
        parseResult.GetValue(option) is { Length: > 0 } value ? new PathOverride(value, option.Name) : null;
}
