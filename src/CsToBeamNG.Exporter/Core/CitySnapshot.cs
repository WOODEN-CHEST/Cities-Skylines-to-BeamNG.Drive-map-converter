using System.Collections.Generic;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// What the exporter read from the running simulation, as plain data. The game-facing code fills it in
/// on the simulation thread in one go, so the values are consistent with each other; everything after
/// that works on this copy.
/// </summary>
internal sealed class CitySnapshot
{
    /// <summary>The city's name (<c>SimulationMetaData.m_CityName</c>), or empty.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>The map the city was started on (<c>SimulationMetaData.m_MapName</c>), or <see langword="null"/>.</summary>
    public string? MapName { get; set; }

    /// <summary>The environment (<c>SimulationMetaData.m_environment</c>), such as <c>Sunny</c>, or <see langword="null"/>.</summary>
    public string? Environment { get; set; }

    /// <summary>The custom map theme's name (<c>MapThemeMetaData.name</c>), or <see langword="null"/> without one.</summary>
    public string? MapTheme { get; set; }

    /// <summary>Whether traffic drives on the left (<c>SimulationMetaData.m_invertTraffic</c>).</summary>
    public bool LeftHandTraffic { get; set; }

    /// <summary>A copy of the grid of unlocked tiles (<c>GameAreaManager.m_areaGrid</c>; see <see cref="Core.AreaGrid"/>), or <see langword="null"/>.</summary>
    public int[]? AreaGrid { get; set; }

    /// <summary>The numbers of objects by kind, in a fixed order.</summary>
    public IList<ObjectCount> Counts { get; } = new List<ObjectCount>();
}
