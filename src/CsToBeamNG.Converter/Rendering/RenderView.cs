using System;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>
/// How a top-down render maps part of the map onto an image. Columns run along +X and rows along −Z, so +X points
/// right and +Z up: the view from above in both games, unmirrored (docs/architecture.md, Coordinate systems).
/// </summary>
/// <remarks>
/// Pixels are square. The longer side of the area gets the requested number of pixels, the shorter side the
/// rounded proportional number, so the image may cover up to half a pixel more or less than the area on that side.
/// Pixel coordinates are continuous: pixel (i, j) covers [i, i + 1) × [j, j + 1), with (0, 0) at the area's
/// −X, +Z corner.
/// </remarks>
public sealed class RenderView
{
    /// <summary>The largest image side, in pixels. An 8192 × 8192 image takes 200 MB.</summary>
    public const int MaxSizePixels = 8192;

    /// <summary>Creates a view.</summary>
    /// <param name="area">The part of the map to show; it must have a width and a depth.</param>
    /// <param name="sizePixels">The number of pixels along the area's longer side, 1 to <see cref="MaxSizePixels"/>.</param>
    /// <exception cref="ArgumentException">The area has no width or depth.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is out of range.</exception>
    public RenderView(MapRect area, int sizePixels)
    {
        if (area.IsEmpty)
        {
            throw new ArgumentException("The area to render must have a width and a depth.", nameof(area));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sizePixels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sizePixels, MaxSizePixels);
        Area = area;
        double width = (double)area.MaxX - area.MinX;
        double depth = (double)area.MaxZ - area.MinZ;
        MetersPerPixel = Math.Max(width, depth) / sizePixels;
        Width = Math.Clamp((int)Math.Round(width / MetersPerPixel), 1, sizePixels);
        Height = Math.Clamp((int)Math.Round(depth / MetersPerPixel), 1, sizePixels);
    }

    /// <summary>The part of the map shown.</summary>
    public MapRect Area { get; }

    /// <summary>The image width in pixels.</summary>
    public int Width { get; }

    /// <summary>The image height in pixels.</summary>
    public int Height { get; }

    /// <summary>The edge length of a pixel, in metres.</summary>
    public double MetersPerPixel { get; }

    /// <summary>Returns the continuous image column of a world X.</summary>
    /// <param name="x">The world X, in metres.</param>
    public double ToPixelX(double x) => (x - Area.MinX) / MetersPerPixel;

    /// <summary>Returns the continuous image row of a world Z.</summary>
    /// <param name="z">The world Z, in metres.</param>
    public double ToPixelY(double z) => (Area.MaxZ - z) / MetersPerPixel;

    /// <summary>Returns the world X of a continuous image column; the centre of column i is at i + 0.5.</summary>
    /// <param name="pixelX">The column.</param>
    public double ToWorldX(double pixelX) => Area.MinX + (pixelX * MetersPerPixel);

    /// <summary>Returns the world Z of a continuous image row; the centre of row j is at j + 0.5.</summary>
    /// <param name="pixelY">The row.</param>
    public double ToWorldZ(double pixelY) => Area.MaxZ - (pixelY * MetersPerPixel);
}
