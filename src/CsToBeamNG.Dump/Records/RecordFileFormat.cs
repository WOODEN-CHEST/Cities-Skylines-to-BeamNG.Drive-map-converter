namespace CsToBeamNG.Dump.Records;

/// <summary>
/// The fixed parts of the record file format: a 64-byte header, then fixed-size records
/// (docs/dump-format.md, Record files).
/// </summary>
public static class RecordFileFormat
{
    /// <summary>The four ASCII characters every record file starts with.</summary>
    public const string Magic = "CSBD";

    /// <summary>The version of the header layout. Readers reject other versions.</summary>
    public const int ContainerVersion = 1;

    /// <summary>The size of the header in bytes, which is also the offset of the first record.</summary>
    public const int HeaderSize = 64;

    /// <summary>The largest record size in bytes that this library writes and reads.</summary>
    public const int MaxRecordSize = 1024 * 1024;

    /// <summary>The longest record type name, in ASCII characters.</summary>
    public const int MaxRecordTypeLength = 40;

    /// <summary>
    /// The record count in the header while the file is written. A file that still has it was never
    /// finished, because the export failed or was interrupted.
    /// </summary>
    public const ulong UnfinishedRecordCount = ulong.MaxValue;

    internal const int VersionOffset = 4;
    internal const int HeaderSizeOffset = 6;
    internal const int FlagsOffset = 8;
    internal const int RecordSizeOffset = 12;
    internal const int RecordCountOffset = 16;
    internal const int RecordTypeOffset = 24;
}
