using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Finds Steam, Cities: Skylines, BeamNG.drive and the folders the conversion needs, on Windows and
/// Linux. What it relies on is described in docs/research/game-locations.md.
/// </summary>
public static class GameEnvironmentLocator
{
    /// <summary>
    /// Runs discovery. It reads the file system (and the registry on Windows), and never changes
    /// anything. Problems are reported in the result; nothing is thrown for a missing game.
    /// </summary>
    /// <param name="context">The machine's locations and the user's overrides.</param>
    /// <returns>Everything found, and the problems.</returns>
    public static GameEnvironment Locate(DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problems = new List<DiscoveryProblem>();
        var paths = new PathTools(context);
        SteamInfo steam = SteamLocator.Locate(context, paths, problems);
        CitiesSkylinesInfo citiesSkylines = CitiesSkylinesLocator.Locate(context, paths, steam, problems);
        BeamNGInfo beamNG = BeamNGLocator.Locate(context, paths, steam, problems);

        return new GameEnvironment
        {
            Os = context.Os,
            Steam = steam,
            CitiesSkylines = citiesSkylines,
            BeamNG = beamNG,
            Problems = problems,
        };
    }
}
