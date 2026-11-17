using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>prop-prefabs</c> section (docs/dump-format.md, <c>prop-prefabs</c>): what
/// <see cref="PropPrefabsSectionWriter"/> writes and the .NET 10 build's <c>PropPrefabsSectionReader</c> returns.
/// </summary>
public sealed class PropPrefabsSectionContent
{
    /// <summary>The prefabs. The writer orders them by index; the reader returns them in the file's order.</summary>
    public IList<PropPrefabRecord> Prefabs { get; } = new List<PropPrefabRecord>();
}
