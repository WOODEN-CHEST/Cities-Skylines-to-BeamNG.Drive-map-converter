using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>
/// Draws on a top-down render in world coordinates (CS1 X and Z, metres) or in pixels. Everything is clipped to
/// the image and drawn without anti-aliasing, with integer and double arithmetic only, so a render is the same on
/// every platform.
/// </summary>
public sealed class MapCanvas
{
    /// <summary>The thickest outline <see cref="StrokeRect"/> draws, in pixels.</summary>
    public const int MaxLineWidthPixels = 64;

    /// <summary>The most corners a polygon for <see cref="FillPolygon"/> and <see cref="BlendPolygon"/> may have.</summary>
    public const int MaxPolygonCorners = 64;

    // Pixel indices are clamped to this range before converting to int. It is far enough outside any image that
    // an edge clamped to it, and an outline band along that edge, stay outside too.
    private const double PixelIndexLimit = 4.0 * RenderView.MaxSizePixels;

    /// <summary>Creates a canvas.</summary>
    /// <param name="image">The image to draw on; its size must be the view's.</param>
    /// <param name="view">How world coordinates map onto the image.</param>
    /// <exception cref="ArgumentException">The image and the view differ in size.</exception>
    public MapCanvas(RgbImage image, RenderView view)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(view);
        if (image.Width != view.Width || image.Height != view.Height)
        {
            throw new ArgumentException("The image must have the view's size.", nameof(image));
        }

        Image = image;
        View = view;
    }

    /// <summary>The image drawn on.</summary>
    public RgbImage Image { get; }

    /// <summary>How world coordinates map onto the image.</summary>
    public RenderView View { get; }

    /// <summary>
    /// Fills a rectangle: every pixel whose centre lies inside it, so that adjacent rectangles neither overlap nor
    /// leave a gap.
    /// </summary>
    /// <param name="rect">The rectangle in world coordinates.</param>
    /// <param name="color">The colour.</param>
    public void FillRect(MapRect rect, Rgb color)
    {
        (int x0, int y0, int x1, int y1) = ToPixels(rect);
        FillPixels(x0, y0, x1, y1, color);
    }

    /// <summary>Lays a translucent colour over a rectangle, covering the same pixels as <see cref="FillRect"/>.</summary>
    /// <param name="rect">The rectangle in world coordinates.</param>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">Its opacity, 0–255.</param>
    public void BlendRect(MapRect rect, Rgb color, byte alpha)
    {
        (int x0, int y0, int x1, int y1) = Clip(ToPixels(rect));
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                Image[x, y] = Image[x, y].Blend(color, alpha);
            }
        }
    }

    /// <summary>Outlines a rectangle along the inside of the pixels <see cref="FillRect"/> would cover.</summary>
    /// <param name="rect">The rectangle in world coordinates.</param>
    /// <param name="color">The colour.</param>
    /// <param name="widthPixels">The outline's width in pixels, 1 to <see cref="MaxLineWidthPixels"/>.</param>
    public void StrokeRect(MapRect rect, Rgb color, int widthPixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(widthPixels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(widthPixels, MaxLineWidthPixels);
        (int x0, int y0, int x1, int y1) = ToPixels(rect);
        if (x1 <= x0 || y1 <= y0)
        {
            return;
        }

        FillPixels(x0, y0, x1, Math.Min(y0 + widthPixels, y1), color);
        FillPixels(x0, Math.Max(y1 - widthPixels, y0), x1, y1, color);
        FillPixels(x0, y0, Math.Min(x0 + widthPixels, x1), y1, color);
        FillPixels(Math.Max(x1 - widthPixels, x0), y0, x1, y1, color);
    }

    /// <summary>
    /// Fills a polygon: every pixel whose centre lies inside it, by the even-odd rule, so that polygons sharing an edge
    /// neither overlap nor leave a gap.
    /// </summary>
    /// <param name="points">The corners in world coordinates (X, Z), in order around the polygon; 3 to <see cref="MaxPolygonCorners"/>.</param>
    /// <param name="color">The colour.</param>
    public void FillPolygon(ReadOnlySpan<(double X, double Z)> points, Rgb color) => ScanPolygon(points, color, byte.MaxValue);

    /// <summary>Lays a translucent colour over a polygon, covering the same pixels as <see cref="FillPolygon"/>.</summary>
    /// <param name="points">The corners in world coordinates (X, Z), in order around the polygon; 3 to <see cref="MaxPolygonCorners"/>.</param>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">Its opacity, 0–255.</param>
    public void BlendPolygon(ReadOnlySpan<(double X, double Z)> points, Rgb color, byte alpha) => ScanPolygon(points, color, alpha);

    /// <summary>Draws a one-pixel line between two world points; the part outside the image is cut off first.</summary>
    /// <param name="x0">The start's X, in metres.</param>
    /// <param name="z0">The start's Z, in metres.</param>
    /// <param name="x1">The end's X, in metres.</param>
    /// <param name="z1">The end's Z, in metres.</param>
    /// <param name="color">The colour.</param>
    public void DrawLine(double x0, double z0, double x1, double z1, Rgb color)
    {
        double startX = View.ToPixelX(x0);
        double startY = View.ToPixelY(z0);
        double endX = View.ToPixelX(x1);
        double endY = View.ToPixelY(z1);
        if (!ClipLine(ref startX, ref startY, ref endX, ref endY))
        {
            return;
        }

        DrawPixelLine(PixelIn(startX, Image.Width), PixelIn(startY, Image.Height), PixelIn(endX, Image.Width), PixelIn(endY, Image.Height), color);
    }

    /// <summary>Draws a one-pixel line between two pixels with Bresenham's algorithm; pixels outside the image are skipped.</summary>
    /// <param name="x0">The start's column.</param>
    /// <param name="y0">The start's row.</param>
    /// <param name="x1">The end's column.</param>
    /// <param name="y1">The end's row.</param>
    /// <param name="color">The colour.</param>
    public void DrawPixelLine(int x0, int y0, int x1, int y1, Rgb color)
    {
        long dx = Math.Abs((long)x1 - x0);
        long dy = -Math.Abs((long)y1 - y0);
        int stepX = x0 < x1 ? 1 : -1;
        int stepY = y0 < y1 ? 1 : -1;
        long error = dx + dy;
        int x = x0;
        int y = y0;
        while (true)
        {
            SetPixel(x, y, color);
            if (x == x1 && y == y1)
            {
                return;
            }

            long doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    /// <summary>Fills the pixels in [x0, x1) × [y0, y1), clipped to the image.</summary>
    /// <param name="x0">The first column.</param>
    /// <param name="y0">The first row.</param>
    /// <param name="x1">The column after the last.</param>
    /// <param name="y1">The row after the last.</param>
    /// <param name="color">The colour.</param>
    public void FillPixels(int x0, int y0, int x1, int y1, Rgb color)
    {
        (x0, y0, x1, y1) = Clip((x0, y0, x1, y1));
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                Image[x, y] = color;
            }
        }
    }

    /// <summary>Sets one pixel if it lies inside the image.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="color">The colour.</param>
    public void SetPixel(int x, int y, Rgb color)
    {
        if (Image.Contains(x, y))
        {
            Image[x, y] = color;
        }
    }

    // Scan conversion: for each pixel row, the polygon's edges cross the line through the row's pixel centres at some
    // points; the pixels whose centres lie between the first and second crossing, the third and fourth, and so on are
    // inside. An edge counts for a row if the row's centre line lies in [its lower end, its upper end), so a corner on the
    // line is counted once and polygons that share an edge don't share pixels.
    private void ScanPolygon(ReadOnlySpan<(double X, double Z)> points, Rgb color, byte alpha)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points.Length, 3, nameof(points));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(points.Length, MaxPolygonCorners, nameof(points));
        int count = points.Length;
        Span<double> xs = stackalloc double[count];
        Span<double> ys = stackalloc double[count];
        Span<double> crossings = stackalloc double[count];
        double minY = double.PositiveInfinity;
        double maxY = double.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            xs[i] = View.ToPixelX(points[i].X);
            ys[i] = View.ToPixelY(points[i].Z);
            if (!(double.IsFinite(xs[i]) && double.IsFinite(ys[i])))
            {
                return;
            }

            minY = Math.Min(minY, ys[i]);
            maxY = Math.Max(maxY, ys[i]);
        }

        int firstRow = Math.Max(FirstCentreAtOrAfter(minY), 0);
        int endRow = Math.Min(FirstCentreAtOrAfter(maxY), Image.Height);
        for (int row = firstRow; row < endRow; row++)
        {
            double centreY = row + 0.5;
            int found = 0;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if ((ys[i] <= centreY && centreY < ys[j]) || (ys[j] <= centreY && centreY < ys[i]))
                {
                    double x = xs[i] + ((centreY - ys[i]) * (xs[j] - xs[i]) / (ys[j] - ys[i]));
                    int k = found++;
                    while (k > 0 && crossings[k - 1] > x)
                    {
                        crossings[k] = crossings[k - 1];
                        k--;
                    }

                    crossings[k] = x;
                }
            }

            for (int k = 0; k + 1 < found; k += 2)
            {
                int start = Math.Max(FirstCentreAtOrAfter(crossings[k]), 0);
                int end = Math.Min(FirstCentreAtOrAfter(crossings[k + 1]), Image.Width);
                for (int column = start; column < end; column++)
                {
                    Image[column, row] = alpha == byte.MaxValue ? color : Image[column, row].Blend(color, alpha);
                }
            }
        }
    }

    // The pixels whose centres lie in the rectangle, as [x0, x1) × [y0, y1), not yet clipped to the image.
    private (int X0, int Y0, int X1, int Y1) ToPixels(MapRect rect) =>
        (FirstCentreAtOrAfter(View.ToPixelX(rect.MinX)),
         FirstCentreAtOrAfter(View.ToPixelY(rect.MaxZ)),
         FirstCentreAtOrAfter(View.ToPixelX(rect.MaxX)),
         FirstCentreAtOrAfter(View.ToPixelY(rect.MinZ)));

    private (int X0, int Y0, int X1, int Y1) Clip((int X0, int Y0, int X1, int Y1) box) =>
        (Math.Max(box.X0, 0), Math.Max(box.Y0, 0), Math.Min(box.X1, Image.Width), Math.Min(box.Y1, Image.Height));

    // The index of the first pixel whose centre, at index + 0.5, is at or after a continuous coordinate.
    private static int FirstCentreAtOrAfter(double coordinate) =>
        (int)Math.Ceiling(Math.Clamp(coordinate - 0.5, -PixelIndexLimit, PixelIndexLimit));

    // The pixel that contains a continuous coordinate within [0, size]; the far edge belongs to the last pixel.
    private static int PixelIn(double coordinate, int size) => Math.Clamp((int)Math.Floor(coordinate), 0, size - 1);

    // Cuts a line in continuous pixel coordinates to the image's rectangle [0, width] × [0, height] (Liang and
    // Barsky's algorithm). Returns false if no part of it lies inside.
    private bool ClipLine(ref double x0, ref double y0, ref double x1, ref double y1)
    {
        if (!(double.IsFinite(x0) && double.IsFinite(y0) && double.IsFinite(x1) && double.IsFinite(y1)))
        {
            return false;
        }

        double dx = x1 - x0;
        double dy = y1 - y0;
        double enter = 0;
        double leave = 1;
        if (!ClipEdge(-dx, x0, ref enter, ref leave)
            || !ClipEdge(dx, Image.Width - x0, ref enter, ref leave)
            || !ClipEdge(-dy, y0, ref enter, ref leave)
            || !ClipEdge(dy, Image.Height - y0, ref enter, ref leave))
        {
            return false;
        }

        double startX = x0;
        double startY = y0;
        x0 = startX + (enter * dx);
        y0 = startY + (enter * dy);
        x1 = startX + (leave * dx);
        y1 = startY + (leave * dy);
        return true;
    }

    // One edge of the clip rectangle: the line is inside where p · t ≤ q.
    private static bool ClipEdge(double p, double q, ref double enter, ref double leave)
    {
        if (p == 0)
        {
            return q >= 0;
        }

        double t = q / p;
        if (p < 0)
        {
            if (t > leave)
            {
                return false;
            }

            enter = Math.Max(enter, t);
        }
        else
        {
            if (t < enter)
            {
                return false;
            }

            leave = Math.Min(leave, t);
        }

        return true;
    }
}
