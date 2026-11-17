using System;
using CsToBeamNG.Converter.Imaging;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws two arrows from the origin, the map's centre: +X pointing right and +Z pointing up. They show at a glance
/// that the image is the unmirrored view from above, and where the origin lies.
/// </summary>
public sealed class AxesLayer : IRenderLayer
{
    /// <summary>The colour of the +X arrow.</summary>
    public static Rgb XColor { get; } = new(220, 30, 30);

    /// <summary>The colour of the +Z arrow.</summary>
    public static Rgb ZColor { get; } = new(30, 70, 230);

    /// <inheritdoc/>
    public string Name => "axes";

    /// <inheritdoc/>
    public string Description => "arrows from the origin at the map's centre: +X in red (right), +Z in blue (up; BeamNG's +Y)";

    /// <summary>The arrows' length in pixels for an image: a sixteenth of its shorter side, at least 8.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    public static int ArrowLength(int width, int height) => Math.Max(8, Math.Min(width, height) / 16);

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MapCanvas canvas = context.Canvas;
        int length = ArrowLength(canvas.Image.Width, canvas.Image.Height);
        double originX = Math.Floor(canvas.View.ToPixelX(0));
        double originY = Math.Floor(canvas.View.ToPixelY(0));
        if (originX < -length || originY < -length || originX > canvas.Image.Width + length || originY > canvas.Image.Height + length)
        {
            return;
        }

        int x = (int)originX;
        int y = (int)originY;
        int head = Math.Max(2, length / 4);

        // Two pixels wide, so the arrows stay visible over busy layers.
        for (int offset = 0; offset < 2; offset++)
        {
            canvas.DrawPixelLine(x, y + offset, x + length, y + offset, XColor);
            canvas.DrawPixelLine(x + length, y + offset, x + length - head, y + offset - head, XColor);
            canvas.DrawPixelLine(x + length, y + offset, x + length - head, y + offset + head, XColor);
            canvas.DrawPixelLine(x + offset, y, x + offset, y - length, ZColor);
            canvas.DrawPixelLine(x + offset, y - length, x + offset - head, y - length + head, ZColor);
            canvas.DrawPixelLine(x + offset, y - length, x + offset + head, y - length + head, ZColor);
        }
    }
}
