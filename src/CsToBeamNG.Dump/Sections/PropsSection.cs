namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>props</c> section: every prop placed on its own (docs/dump-format.md, <c>props</c>; docs/research/cs1-assets.md,
/// Placement). <see cref="PropsSectionWriter"/> writes it; <c>PropsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class PropsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "props";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The props, ordered by id.</summary>
    public const string PropsFileName = "props.bin";

    /// <summary>The record type of <see cref="PropsFileName"/>.</summary>
    public const string PropRecordType = "prop";

    /// <summary>The size of a <see cref="PropRecordType"/> record in bytes.</summary>
    public const int PropRecordSize = 48;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
