using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>net-prefabs</c> section (docs/dump-format.md, <c>net-prefabs</c>): what
/// <see cref="NetPrefabsSectionWriter"/> writes and the .NET 10 build's <c>NetPrefabsSectionReader</c> returns.
/// </summary>
public sealed class NetPrefabsSectionContent
{
    /// <summary>The prefabs. The writer orders them by index; the reader returns them in the file's order.</summary>
    public IList<NetPrefabRecord> Prefabs { get; } = new List<NetPrefabRecord>();
}
