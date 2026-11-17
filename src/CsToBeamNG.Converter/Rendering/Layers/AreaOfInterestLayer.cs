using System;
using CsToBeamNG.Converter.Imaging;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>Outlines the area of interest: the part of the map the conversion covers.</summary>
public sealed class AreaOfInterestLayer : IRenderLayer
{
    /// <summary>The outline's width in pixels.</summary>
    public const int WidthPixels = 2;

    /// <summary>The outline's colour.</summary>
    public static Rgb Color { get; } = new(255, 120, 0);

    /// <inheritdoc/>
    public string Name => "area";

    /// <inheritdoc/>
    public string Description => "the area of interest outlined in orange";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Canvas.StrokeRect(context.AreaOfInterest.Bounds, Color, WidthPixels);
    }
}
