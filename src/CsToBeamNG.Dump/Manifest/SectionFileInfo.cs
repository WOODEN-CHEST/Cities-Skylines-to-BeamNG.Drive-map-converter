namespace CsToBeamNG.Dump.Manifest;

/// <summary>A file of a section.</summary>
public sealed class SectionFileInfo
{
    /// <summary>Creates an empty instance, to fill in.</summary>
    public SectionFileInfo()
    {
    }

    /// <summary>Creates an instance.</summary>
    /// <param name="path">The path relative to the section's folder.</param>
    /// <param name="bytes">The file's size in bytes.</param>
    public SectionFileInfo(string path, long bytes)
    {
        Path = path;
        Bytes = bytes;
    }

    /// <summary>
    /// The path relative to the section's folder, with <c>/</c> between folders, such as <c>nodes.bin</c>
    /// (docs/dump-format.md, Names). Required.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The file's size in bytes. Readers check it, which catches truncated files.</summary>
    public long Bytes { get; set; }
}
