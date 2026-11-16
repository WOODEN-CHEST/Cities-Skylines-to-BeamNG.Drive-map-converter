using System.Collections.Generic;
using System.Linq;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>Everything discovery found: Steam, both games, their folders, and the problems.</summary>
public sealed record GameEnvironment
{
    /// <summary>The OS whose conventions were applied.</summary>
    public required OsKind Os { get; init; }

    /// <summary>The Steam installations.</summary>
    public required SteamInfo Steam { get; init; }

    /// <summary>Cities: Skylines.</summary>
    public required CitiesSkylinesInfo CitiesSkylines { get; init; }

    /// <summary>BeamNG.drive.</summary>
    public required BeamNGInfo BeamNG { get; init; }

    /// <summary>Problems and notes, in the order they were found: Steam, then Cities: Skylines, then BeamNG.drive.</summary>
    public required IReadOnlyList<DiscoveryProblem> Problems { get; init; }

    /// <summary>Whether something essential is missing, so that conversion can't work.</summary>
    public bool HasErrors => Problems.Any(problem => problem.Severity == ProblemSeverity.Error);
}
