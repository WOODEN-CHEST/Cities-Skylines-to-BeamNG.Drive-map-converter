using System;
using System.Globalization;

namespace CsToBeamNG.Converter.Imaging;

/// <summary>
/// An opaque image with 8-bit RGB pixels, stored row by row from the top row, three bytes per pixel. Pixel (0, 0)
/// is the top-left corner.
/// </summary>
public sealed class RgbImage
{
    /// <summary>The bytes per pixel: red, green and blue.</summary>
    public const int BytesPerPixel = 3;

    private readonly byte[] _pixels;

    /// <summary>Creates an image filled with one colour.</summary>
    /// <param name="width">The width in pixels, 1 or more.</param>
    /// <param name="height">The height in pixels, 1 or more.</param>
    /// <param name="fill">The colour of every pixel.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is less than 1, or the image would be too large for an array.</exception>
    public RgbImage(int width, int height, Rgb fill)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        long byteCount = (long)width * height * BytesPerPixel;
        if (byteCount > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                string.Create(CultureInfo.InvariantCulture, $"A {width} × {height} image is too large."));
        }

        Width = width;
        Height = height;
        _pixels = new byte[byteCount];
        for (int i = 0; i < _pixels.Length; i += BytesPerPixel)
        {
            _pixels[i] = fill.R;
            _pixels[i + 1] = fill.G;
            _pixels[i + 2] = fill.B;
        }
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>The pixels: row by row from the top, red, green and blue for each.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>Gets or sets a pixel.</summary>
    /// <param name="x">The column, 0 at the left.</param>
    /// <param name="y">The row, 0 at the top.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pixel is outside the image.</exception>
    public Rgb this[int x, int y]
    {
        get
        {
            int offset = Offset(x, y);
            return new Rgb(_pixels[offset], _pixels[offset + 1], _pixels[offset + 2]);
        }

        set
        {
            int offset = Offset(x, y);
            _pixels[offset] = value.R;
            _pixels[offset + 1] = value.G;
            _pixels[offset + 2] = value.B;
        }
    }

    /// <summary>Whether a pixel lies inside the image.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    private int Offset(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                string.Create(CultureInfo.InvariantCulture, $"Pixel ({x}, {y}) is outside the {Width} × {Height} image."));
        }

        return ((y * Width) + x) * BytesPerPixel;
    }
}
