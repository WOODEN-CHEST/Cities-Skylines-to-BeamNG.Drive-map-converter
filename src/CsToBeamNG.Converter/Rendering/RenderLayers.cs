using System.Collections.Generic;
using CsToBeamNG.Converter.Rendering.Layers;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>The layers of the top-down debug render.</summary>
public static class RenderLayers
{
    /// <summary>Creates the default layers, bottom to top.</summary>
    /// <remarks>
    /// The export steps add theirs in these places: terrain shading and water right above the ground, so the tile
    /// grid stays visible over them; roads, building footprints and trees above the grid; the area of interest and
    /// the axes stay on top.
    /// </remarks>
    public static IReadOnlyList<IRenderLayer> CreateDefault() =>
    [
        new GroundLayer(),
        new HillshadeLayer(),
        new WaterLayer(),
        new TileGridLayer(),
        new AreaOfInterestLayer(),
        new AxesLayer(),
    ];
}
