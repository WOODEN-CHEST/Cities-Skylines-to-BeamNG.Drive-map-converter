using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>trees</c> section (docs/dump-format.md, <c>trees</c>): what <see cref="TreesSectionWriter"/>
/// writes and the .NET 10 build's <c>TreesSectionReader</c> returns.
/// </summary>
public sealed class TreesSectionContent
{
    /// <summary>The trees. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<TreeRecord> Trees { get; } = new List<TreeRecord>();
}
