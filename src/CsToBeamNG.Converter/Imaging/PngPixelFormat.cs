namespace CsToBeamNG.Converter.Imaging;

/// <summary>The pixel layouts that <see cref="PngEncoder"/> writes, all with 8 bits per channel.</summary>
public enum PngPixelFormat
{
    /// <summary>One byte per pixel: grey (PNG colour type 0).</summary>
    Gray8,

    /// <summary>Three bytes per pixel: red, green, blue (PNG colour type 2).</summary>
    Rgb24,

    /// <summary>Four bytes per pixel: red, green, blue, alpha, not premultiplied (PNG colour type 6).</summary>
    Rgba32,
}
