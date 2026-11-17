namespace CsToBeamNG.Converter.Automation;

/// <summary>How an <see cref="UnattendedExport"/> ended.</summary>
/// <param name="Succeeded">Whether a complete dump was written.</param>
/// <param name="Message">What happened, for the user.</param>
/// <param name="DumpFolder">The dump's folder, if the exporter created one.</param>
public sealed record UnattendedExportResult(bool Succeeded, string Message, string? DumpFolder);
