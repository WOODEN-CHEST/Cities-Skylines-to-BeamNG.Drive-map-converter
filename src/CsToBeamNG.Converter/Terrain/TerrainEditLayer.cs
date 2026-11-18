using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// Edits to the terrain's heights that later stages add, such as road beds and building lots, as an ordered list of
/// <see cref="TerrainStamp"/>s. <see cref="TerrainHeightField.Apply"/> applies them after the city's ground has been
/// sampled, each to the result of the ones before it (docs/architecture.md, D37).
/// </summary>
public sealed class TerrainEditLayer
{
    private readonly List<TerrainStamp> _stamps = [];

    /// <summary>The stamps, in the order they are applied.</summary>
    public IReadOnlyList<TerrainStamp> Stamps => _stamps;

    /// <summary>Adds a stamp after the existing ones.</summary>
    /// <param name="stamp">The stamp.</param>
    public void Add(TerrainStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        _stamps.Add(stamp);
    }
}
