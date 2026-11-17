namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>net-segments</c> section: every segment of the game's networks, with the centre curve and the corners the
/// game computes (docs/dump-format.md, <c>net-segments</c>; docs/research/cs1-networks.md, Data model and Corners).
/// <see cref="NetSegmentsSectionWriter"/> writes it; <c>NetSegmentsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class NetSegmentsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "net-segments";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The segments, ordered by id.</summary>
    public const string SegmentsFileName = "segments.bin";

    /// <summary>The record type of <see cref="SegmentsFileName"/>.</summary>
    public const string SegmentRecordType = "netSegment";

    /// <summary>The size of a <see cref="SegmentRecordType"/> record in bytes.</summary>
    public const int SegmentRecordSize = 212;

    /// <summary>The bit of a segment record's <c>cornerFlags</c> that holds the start's left corner's smoothness.</summary>
    public const byte StartLeftSmoothBit = 0x1;

    /// <summary>The bit of a segment record's <c>cornerFlags</c> that holds the start's right corner's smoothness.</summary>
    public const byte StartRightSmoothBit = 0x2;

    /// <summary>The bit of a segment record's <c>cornerFlags</c> that holds the end's left corner's smoothness.</summary>
    public const byte EndLeftSmoothBit = 0x4;

    /// <summary>The bit of a segment record's <c>cornerFlags</c> that holds the end's right corner's smoothness.</summary>
    public const byte EndRightSmoothBit = 0x8;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
