namespace CsToBeamNG.Converter.Discovery;

/// <summary>A problem or note from discovery.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">A message for the user, naming what is affected and what to do about it.</param>
public sealed record DiscoveryProblem(ProblemSeverity Severity, string Message);
