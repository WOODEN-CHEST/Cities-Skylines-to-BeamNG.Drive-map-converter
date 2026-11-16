using System.Collections.Generic;
using System.IO;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>A place where a folder may be, in order of preference.</summary>
internal sealed record Candidate(string Path, LocationSource Source, string? Detail = null)
{
    /// <summary>
    /// Picks the first candidate folder that exists, and reports every other existing one as a note.
    /// If none exists, returns the first candidate, marked as missing, so that the caller can say
    /// where the folder was expected.
    /// </summary>
    /// <returns>The chosen folder, or <see langword="null"/> if there are no candidates.</returns>
    public static FoundPath? ChooseFirstExisting(
        IReadOnlyList<Candidate> candidates,
        string what,
        PathTools paths,
        List<DiscoveryProblem> problems)
    {
        FoundPath? chosen = null;
        foreach (Candidate candidate in candidates)
        {
            if (!Directory.Exists(candidate.Path))
            {
                continue;
            }

            if (chosen is null)
            {
                chosen = new FoundPath(candidate.Path, candidate.Source, Exists: true, candidate.Detail);
            }
            else if (!paths.AreSame(paths.Canonicalize(candidate.Path), paths.Canonicalize(chosen.Path)))
            {
                string where = candidate.Detail is null ? string.Empty : $" ({candidate.Detail})";
                problems.AddInfo($"{what}: another one exists at '{candidate.Path}'{where}; using '{chosen.Path}'.");
            }
        }

        if (chosen is not null || candidates.Count == 0)
        {
            return chosen;
        }

        Candidate expected = candidates[0];
        return new FoundPath(expected.Path, expected.Source, Exists: false, expected.Detail);
    }
}
