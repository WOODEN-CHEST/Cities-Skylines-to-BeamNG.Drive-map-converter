#if NET
namespace CsToBeamNG.Dump;

/// <summary>How important a <see cref="DumpNotice"/> is.</summary>
public enum DumpNoticeSeverity
{
    /// <summary>For information, such as a newer minor format version.</summary>
    Info,

    /// <summary>The conversion may lack data, such as from an export that didn't finish.</summary>
    Warning,
}
#endif
