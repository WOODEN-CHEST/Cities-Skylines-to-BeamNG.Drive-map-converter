using System.Collections.Generic;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>
/// A section of a dump: a named, separately versioned part, such as the terrain, whose files are in the
/// folder of the same name (docs/dump-format.md, Layout).
/// </summary>
public sealed class SectionInfo
{
    /// <summary>The section's name, which is also its folder's name, such as <c>net-nodes</c>. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The version of the section's content. Readers read only their own major version.</summary>
    public DumpVersion Version { get; set; }

    /// <summary>
    /// The number of items the section describes, as its specification defines them (such as nodes or
    /// prefabs), or <see langword="null"/> if it doesn't say. For summaries; readers don't rely on it.
    /// </summary>
    public long? RecordCount { get; set; }

    /// <summary>The section's files, with paths relative to its folder.</summary>
    public IList<SectionFileInfo> Files { get; } = new List<SectionFileInfo>();
}
