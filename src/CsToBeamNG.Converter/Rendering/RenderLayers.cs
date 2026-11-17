using System.Collections.Generic;
using CsToBeamNG.Converter.Rendering.Layers;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>The layers of the top-down debug render.</summary>
public static class RenderLayers
{
    /// <summary>Creates the default layers, bottom to top.</summary>
    /// <remarks>
    /// Terrain shading and water lie right above the ground, so the tile grid stays visible over them; the networks,
    /// trees, building lots and props lie above the grid, trees below the lots, which the game keeps free of them; the
    /// area of interest and the axes stay on top.
    /// </remarks>
    public static IReadOnlyList<IRenderLayer> CreateDefault() =>
    [
        new GroundLayer(),
        new HillshadeLayer(),
        new WaterLayer(),
        new TileGridLayer(),
        new NetworkLayer(),
        new TreeLayer(),
        new BuildingLayer(),
        new PropLayer(),
        new AreaOfInterestLayer(),
        new AxesLayer(),
    ];
}
