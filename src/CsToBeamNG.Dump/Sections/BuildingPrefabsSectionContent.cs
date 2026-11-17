using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>building-prefabs</c> section (docs/dump-format.md, <c>building-prefabs</c>): what
/// <see cref="BuildingPrefabsSectionWriter"/> writes and the .NET 10 build's <c>BuildingPrefabsSectionReader</c> returns.
/// </summary>
public sealed class BuildingPrefabsSectionContent
{
    /// <summary>The prefabs. The writer orders them by index; the reader returns them in the file's order.</summary>
    public IList<BuildingPrefabRecord> Prefabs { get; } = new List<BuildingPrefabRecord>();
}
