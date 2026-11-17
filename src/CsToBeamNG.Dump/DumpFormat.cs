namespace CsToBeamNG.Dump;

/// <summary>
/// The version and fixed names of the city dump format that the exporter writes and the converter reads.
/// </summary>
/// <remarks>
/// The format is specified in <c>docs/dump-format.md</c>. This version covers the container: the folder
/// layout, the manifest, record files, string tables and the JSON conventions. Sections have versions
/// of their own (<see cref="Manifest.SectionInfo.Version"/>). Readers accept data with the same major
/// version; minor versions only add optional data.
/// </remarks>
public static class DumpFormat
{
    /// <summary>The major version. It changes when a change breaks existing readers.</summary>
    public const int MajorVersion = 0;

    /// <summary>The minor version. It changes when optional data is added.</summary>
    public const int MinorVersion = 1;

    /// <summary>The name of the manifest file in the dump folder.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>The format version that this library writes. It reads every version with the same major version.</summary>
    public static DumpVersion CurrentVersion => new(MajorVersion, MinorVersion);
}
