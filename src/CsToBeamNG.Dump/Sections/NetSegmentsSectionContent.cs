using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>net-segments</c> section (docs/dump-format.md, <c>net-segments</c>): what
/// <see cref="NetSegmentsSectionWriter"/> writes and the .NET 10 build's <c>NetSegmentsSectionReader</c> returns.
/// </summary>
public sealed class NetSegmentsSectionContent
{
    /// <summary>The segments. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<NetSegmentRecord> Segments { get; } = new List<NetSegmentRecord>();
}
