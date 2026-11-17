namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>trees</c> section: every tree of the city and the map (docs/dump-format.md, <c>trees</c>;
/// docs/research/cs1-assets.md, Placement). <see cref="TreesSectionWriter"/> writes it; <c>TreesSectionReader</c> reads
/// it in the .NET 10 build.
/// </summary>
public static class TreesSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "trees";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The trees, ordered by id.</summary>
    public const string TreesFileName = "trees.bin";

    /// <summary>The record type of <see cref="TreesFileName"/>.</summary>
    public const string TreeRecordType = "tree";

    /// <summary>The size of a <see cref="TreeRecordType"/> record in bytes.</summary>
    public const int TreeRecordSize = 32;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
