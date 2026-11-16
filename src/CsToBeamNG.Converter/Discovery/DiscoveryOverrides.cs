using System;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Locations given by the user. Each one replaces the search for that location; a missing
/// override path is an error, never a reason to fall back to searching.
/// </summary>
public sealed record DiscoveryOverrides
{
    /// <summary>The environment variable for <see cref="SteamDir"/>.</summary>
    public const string SteamDirVariable = "CSTOBEAMNG_STEAM_DIR";

    /// <summary>The environment variable for <see cref="CitiesSkylinesDir"/>.</summary>
    public const string CitiesSkylinesDirVariable = "CSTOBEAMNG_CS1_DIR";

    /// <summary>The environment variable for <see cref="CitiesSkylinesUserDir"/>.</summary>
    public const string CitiesSkylinesUserDirVariable = "CSTOBEAMNG_CS1_USER_DIR";

    /// <summary>The environment variable for <see cref="BeamNGDir"/>.</summary>
    public const string BeamNGDirVariable = "CSTOBEAMNG_BEAMNG_DIR";

    /// <summary>The environment variable for <see cref="BeamNGUserDir"/>.</summary>
    public const string BeamNGUserDirVariable = "CSTOBEAMNG_BEAMNG_USER_DIR";

    /// <summary>The Steam folder, the one that contains <c>steamapps</c>. Replaces the search for Steam.</summary>
    public PathOverride? SteamDir { get; init; }

    /// <summary>The Cities: Skylines install folder, the one that contains <c>Cities_Data</c>.</summary>
    public PathOverride? CitiesSkylinesDir { get; init; }

    /// <summary>The Cities: Skylines user data folder, the one that contains <c>Addons</c> and <c>Saves</c>.</summary>
    public PathOverride? CitiesSkylinesUserDir { get; init; }

    /// <summary>The BeamNG.drive install folder.</summary>
    public PathOverride? BeamNGDir { get; init; }

    /// <summary>The BeamNG.drive user folder itself: the <c>current</c> folder in the default layout.</summary>
    public PathOverride? BeamNGUserDir { get; init; }

    /// <summary>Reads the overrides from the <c>CSTOBEAMNG_*</c> environment variables.</summary>
    /// <param name="getVariable">Returns the value of an environment variable, or <see langword="null"/>.</param>
    /// <returns>The overrides; empty values count as unset.</returns>
    public static DiscoveryOverrides FromEnvironment(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        return new DiscoveryOverrides
        {
            SteamDir = Read(SteamDirVariable),
            CitiesSkylinesDir = Read(CitiesSkylinesDirVariable),
            CitiesSkylinesUserDir = Read(CitiesSkylinesUserDirVariable),
            BeamNGDir = Read(BeamNGDirVariable),
            BeamNGUserDir = Read(BeamNGUserDirVariable),
        };

        PathOverride? Read(string name)
        {
            string? value = getVariable(name);
            return string.IsNullOrWhiteSpace(value) ? null : new PathOverride(value.Trim(), name);
        }
    }

    /// <summary>Combines two sets of overrides.</summary>
    /// <param name="higherPriority">Overrides that win where both set a location, such as command-line options over environment variables.</param>
    /// <returns>A copy of these overrides with every location that <paramref name="higherPriority"/> sets taken from it.</returns>
    public DiscoveryOverrides OverriddenBy(DiscoveryOverrides higherPriority)
    {
        ArgumentNullException.ThrowIfNull(higherPriority);

        return new DiscoveryOverrides
        {
            SteamDir = higherPriority.SteamDir ?? SteamDir,
            CitiesSkylinesDir = higherPriority.CitiesSkylinesDir ?? CitiesSkylinesDir,
            CitiesSkylinesUserDir = higherPriority.CitiesSkylinesUserDir ?? CitiesSkylinesUserDir,
            BeamNGDir = higherPriority.BeamNGDir ?? BeamNGDir,
            BeamNGUserDir = higherPriority.BeamNGUserDir ?? BeamNGUserDir,
        };
    }
}
