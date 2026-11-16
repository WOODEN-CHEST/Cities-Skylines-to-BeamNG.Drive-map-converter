namespace CsToBeamNG.Dump;

/// <summary>
/// The version of the city dump format that the exporter writes and the converter reads.
/// </summary>
/// <remarks>
/// The format is specified in <c>docs/dump-format.md</c>. Readers accept dumps with the same major
/// version; minor versions only add optional data.
/// </remarks>
public static class DumpFormat
{
    /// <summary>The major version. It changes when a change breaks existing readers.</summary>
    public const int MajorVersion = 0;

    /// <summary>The minor version. It changes when optional sections or fields are added.</summary>
    public const int MinorVersion = 0;
}
