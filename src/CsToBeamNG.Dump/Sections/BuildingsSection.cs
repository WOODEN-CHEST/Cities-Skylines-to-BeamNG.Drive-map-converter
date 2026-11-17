namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>buildings</c> section: every building of the city, sub-buildings included (docs/dump-format.md,
/// <c>buildings</c>; docs/research/cs1-assets.md, Placement). <see cref="BuildingsSectionWriter"/> writes it;
/// <c>BuildingsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class BuildingsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "buildings";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The buildings, ordered by id.</summary>
    public const string BuildingsFileName = "buildings.bin";

    /// <summary>The record type of <see cref="BuildingsFileName"/>.</summary>
    public const string BuildingRecordType = "building";

    /// <summary>The size of a <see cref="BuildingRecordType"/> record in bytes.</summary>
    public const int BuildingRecordSize = 72;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
