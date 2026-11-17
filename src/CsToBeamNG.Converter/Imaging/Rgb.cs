namespace CsToBeamNG.Converter.Imaging;

/// <summary>An opaque colour with 8 bits per channel, in sRGB.</summary>
/// <param name="R">Red, 0–255.</param>
/// <param name="G">Green, 0–255.</param>
/// <param name="B">Blue, 0–255.</param>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>
    /// Returns this colour with another one laid over it: alpha 0 keeps this colour, 255 gives the other. Integer
    /// arithmetic, rounded to the nearest value, so the result is the same everywhere.
    /// </summary>
    /// <param name="over">The colour on top.</param>
    /// <param name="alpha">The opacity of <paramref name="over"/>, 0–255.</param>
    public Rgb Blend(Rgb over, byte alpha) => new(Mix(R, over.R, alpha), Mix(G, over.G, alpha), Mix(B, over.B, alpha));

    private static byte Mix(byte under, byte over, byte alpha) => (byte)(((under * (255 - alpha)) + (over * alpha) + 127) / 255);
}
