using System.Collections.Generic;

namespace CsToBeamNG.Converter.Discovery;

internal static class ProblemListExtensions
{
    public static void AddError(this List<DiscoveryProblem> problems, string message) =>
        problems.Add(new DiscoveryProblem(ProblemSeverity.Error, message));

    public static void AddWarning(this List<DiscoveryProblem> problems, string message) =>
        problems.Add(new DiscoveryProblem(ProblemSeverity.Warning, message));

    public static void AddInfo(this List<DiscoveryProblem> problems, string message) =>
        problems.Add(new DiscoveryProblem(ProblemSeverity.Info, message));
}
