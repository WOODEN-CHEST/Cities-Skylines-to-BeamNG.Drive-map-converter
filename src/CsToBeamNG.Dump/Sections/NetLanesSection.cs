namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>net-lanes</c> section: every lane of the game's network segments (docs/dump-format.md, <c>net-lanes</c>;
/// docs/research/cs1-networks.md, Lanes). <see cref="NetLanesSectionWriter"/> writes it; <c>NetLanesSectionReader</c>
/// reads it in the .NET 10 build.
/// </summary>
public static class NetLanesSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "net-lanes";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The lanes, ordered by id.</summary>
    public const string LanesFileName = "lanes.bin";

    /// <summary>The record type of <see cref="LanesFileName"/>.</summary>
    public const string LaneRecordType = "netLane";

    /// <summary>The size of a <see cref="LaneRecordType"/> record in bytes.</summary>
    public const int LaneRecordSize = 64;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
