using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>Fills the map's extent with a plain ground colour, so the map's edges show against the background.</summary>
public sealed class GroundLayer : IRenderLayer
{
    /// <summary>The ground's colour.</summary>
    public static Rgb Color { get; } = new(222, 216, 200);

    /// <inheritdoc/>
    public string Name => "ground";

    /// <inheritdoc/>
    public string Description => "the map's 17.28 km square in beige; outside it dark grey";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Canvas.FillRect(MapGeometry.Bounds, Color);
    }
}
