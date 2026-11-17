#if NET
namespace CsToBeamNG.Dump;

/// <summary>Something <see cref="DumpReader"/> noticed while opening a dump that doesn't stop it from being read.</summary>
/// <param name="Severity">How important it is.</param>
/// <param name="Message">A message for the user.</param>
public sealed record DumpNotice(DumpNoticeSeverity Severity, string Message);
#endif
