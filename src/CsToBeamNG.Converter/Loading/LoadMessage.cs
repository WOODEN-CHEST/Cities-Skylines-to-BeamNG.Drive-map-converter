namespace CsToBeamNG.Converter.Loading;

/// <summary>Something noticed while loading a dump, for the user: a skipped section, a damaged object, an old dump.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">A message for the user.</param>
/// <param name="SectionName">The section it concerns, or <see langword="null"/> if it concerns the whole dump.</param>
public sealed record LoadMessage(LoadMessageSeverity Severity, string Message, string? SectionName = null);
