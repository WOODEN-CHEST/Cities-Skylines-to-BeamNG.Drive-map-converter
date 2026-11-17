using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Cli;

/// <summary>What <c>cstobeamng inspect</c> was asked to do.</summary>
/// <param name="DumpPath">The dump folder, or its manifest.json.</param>
internal sealed record InspectRequest(string DumpPath)
{
    /// <summary>Tiles that override the area of interest, or <see langword="null"/>.</summary>
    public IReadOnlyList<TileCoordinate>? Tiles { get; init; }

    /// <summary>The PNG file to draw into, or <see langword="null"/> for no image.</summary>
    public string? RenderPath { get; init; }

    /// <summary>The image's longer side, in pixels.</summary>
    public int RenderSize { get; init; } = InspectCommand.DefaultRenderSize;

    /// <summary>The part of the map to draw.</summary>
    public RenderArea RenderArea { get; init; } = RenderArea.AreaOfInterest;
}
