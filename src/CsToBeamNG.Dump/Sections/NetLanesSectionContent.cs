using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>net-lanes</c> section (docs/dump-format.md, <c>net-lanes</c>): what
/// <see cref="NetLanesSectionWriter"/> writes and the .NET 10 build's <c>NetLanesSectionReader</c> returns.
/// </summary>
public sealed class NetLanesSectionContent
{
    /// <summary>The lanes. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<NetLaneRecord> Lanes { get; } = new List<NetLaneRecord>();
}
