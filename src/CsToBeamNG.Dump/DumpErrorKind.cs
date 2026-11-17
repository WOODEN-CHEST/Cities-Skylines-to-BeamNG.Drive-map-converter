namespace CsToBeamNG.Dump;

/// <summary>Why a dump or one of its files can't be read.</summary>
public enum DumpErrorKind
{
    /// <summary>The dump folder, its manifest or another file doesn't exist.</summary>
    NotFound,

    /// <summary>
    /// The data is damaged or malformed: invalid JSON, a missing field, a wrong header, a truncated file,
    /// a size that doesn't match the manifest.
    /// </summary>
    Corrupt,

    /// <summary>The data was written in a version that this reader doesn't read.</summary>
    Incompatible,

    /// <summary>The export didn't finish: the manifest says so, or a record file was never completed.</summary>
    Incomplete,
}
