#if NET
namespace CsToBeamNG.Dump;

/// <summary>Options for <see cref="DumpReader.Open"/>.</summary>
public sealed record DumpReadOptions
{
    /// <summary>The defaults: incomplete dumps are rejected.</summary>
    public static DumpReadOptions Default { get; } = new();

    /// <summary>
    /// Whether to open a dump whose export didn't finish (<c>complete: false</c>), with a warning notice
    /// instead of an error. For diagnostics; such a dump may lack data.
    /// </summary>
    public bool AllowIncomplete { get; init; }
}
#endif
