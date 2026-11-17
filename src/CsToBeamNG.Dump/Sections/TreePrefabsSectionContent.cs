using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>tree-prefabs</c> section (docs/dump-format.md, <c>tree-prefabs</c>): what
/// <see cref="TreePrefabsSectionWriter"/> writes and the .NET 10 build's <c>TreePrefabsSectionReader</c> returns.
/// </summary>
public sealed class TreePrefabsSectionContent
{
    /// <summary>The prefabs. The writer orders them by index; the reader returns them in the file's order.</summary>
    public IList<TreePrefabRecord> Prefabs { get; } = new List<TreePrefabRecord>();
}
