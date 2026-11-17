using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>props</c> section (docs/dump-format.md, <c>props</c>): what <see cref="PropsSectionWriter"/>
/// writes and the .NET 10 build's <c>PropsSectionReader</c> returns.
/// </summary>
public sealed class PropsSectionContent
{
    /// <summary>The props. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<PropRecord> Props { get; } = new List<PropRecord>();
}
