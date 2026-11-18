using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Converter.Planning;

namespace CsToBeamNG.Cli;

/// <summary>What <c>cstobeamng convert</c> was asked to do.</summary>
/// <param name="DumpPath">The dump folder, or its manifest.json.</param>
internal sealed record ConvertRequest(string DumpPath)
{
    /// <summary>The level's name as given with <c>--name</c>, or <see langword="null"/> for the city's name.</summary>
    public string? Name { get; init; }

    /// <summary>The part of the map to convert.</summary>
    public RenderArea Area { get; init; } = RenderArea.AreaOfInterest;

    /// <summary>The most terrain samples per side.</summary>
    public int MaxTerrainSize { get; init; } = TerrainGridOptions.Default.MaxSize;

    /// <summary>Overrides of the game locations.</summary>
    public DiscoveryOverrides Locations { get; init; } = new();
}
