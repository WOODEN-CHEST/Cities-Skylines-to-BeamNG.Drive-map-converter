using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>buildings</c> section (docs/dump-format.md, <c>buildings</c>): what
/// <see cref="BuildingsSectionWriter"/> writes and the .NET 10 build's <c>BuildingsSectionReader</c> returns.
/// </summary>
public sealed class BuildingsSectionContent
{
    /// <summary>The buildings. The writer orders them by id; the reader returns them in the file's order.</summary>
    public IList<BuildingRecord> Buildings { get; } = new List<BuildingRecord>();
}
