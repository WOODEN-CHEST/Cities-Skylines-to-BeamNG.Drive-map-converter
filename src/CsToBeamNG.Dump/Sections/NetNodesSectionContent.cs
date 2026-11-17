using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>net-nodes</c> section (docs/dump-format.md, <c>net-nodes</c>): what
/// <see cref="NetNodesSectionWriter"/> writes and the .NET 10 build's <c>NetNodesSectionReader</c> returns.
/// </summary>
public sealed class NetNodesSectionContent
{
    /// <summary>The nodes. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<NetNodeRecord> Nodes { get; } = new List<NetNodeRecord>();
}
