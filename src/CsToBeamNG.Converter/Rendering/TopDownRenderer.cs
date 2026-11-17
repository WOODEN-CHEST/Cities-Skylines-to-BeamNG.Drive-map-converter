using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>
/// Draws a top-down debug image of a city: north (+Z) up, east (+X) right, as seen from above in both games. It is
/// the quickest check of the layout and alignment of what the converter loaded (agents/verification.md).
/// </summary>
public static class TopDownRenderer
{
    /// <summary>The colour of everything no layer draws on, such as the space outside the map.</summary>
    public static Rgb Background { get; } = new(40, 40, 40);

    /// <summary>Renders a city.</summary>
    /// <param name="world">The world model.</param>
    /// <param name="areaOfInterest">The planned area of interest, which a layer outlines.</param>
    /// <param name="view">The part of the map to draw and the image size.</param>
    /// <param name="layers">The layers, bottom to top, or <see langword="null"/> for <see cref="RenderLayers.CreateDefault"/>.</param>
    /// <returns>The image.</returns>
    public static RgbImage Render(CityWorld world, AreaOfInterest areaOfInterest, RenderView view, IEnumerable<IRenderLayer>? layers = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var image = new RgbImage(view.Width, view.Height, Background);
        var context = new RenderContext(world, areaOfInterest, new MapCanvas(image, view));
        foreach (IRenderLayer layer in layers ?? RenderLayers.CreateDefault())
        {
            layer.Draw(context);
        }

        return image;
    }
}
