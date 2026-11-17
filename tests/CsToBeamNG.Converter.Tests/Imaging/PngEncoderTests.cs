using System;
using System.IO;
using CsToBeamNG.Converter.Imaging;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Imaging;

public sealed class PngEncoderTests
{
    [Theory]
    [InlineData(PngPixelFormat.Gray8, 1, 0)]
    [InlineData(PngPixelFormat.Rgb24, 3, 2)]
    [InlineData(PngPixelFormat.Rgba32, 4, 6)]
    public void WritesPixelsThatDecodeExactly(PngPixelFormat format, int channels, int colorType)
    {
        const int Width = 37;
        const int Height = 23;
        byte[] pixels = TestPattern(Width, Height, channels);

        PngTestImage decoded = PngTestDecoder.Decode(Encode(Width, Height, pixels, format));

        Assert.Equal(Width, decoded.Width);
        Assert.Equal(Height, decoded.Height);
        Assert.Equal(colorType, decoded.ColorType);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void ChoosesAFilterPerRow()
    {
        // Flat rows, gradients and noise favour different filters; all of them must decode.
        byte[] pixels = TestPattern(64, 64, 3);

        PngTestImage decoded = PngTestDecoder.Decode(Encode(64, 64, pixels, PngPixelFormat.Rgb24));

        Assert.True(decoded.FiltersUsed.Count >= 3, "filters used: " + string.Join(", ", decoded.FiltersUsed));
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void WritesAnRgbImage()
    {
        var image = new RgbImage(2, 2, new Rgb(10, 20, 30));
        image[1, 0] = new Rgb(255, 0, 0);
        image[0, 1] = new Rgb(0, 255, 0);
        using var stream = new MemoryStream();

        PngEncoder.Write(stream, image);

        PngTestImage decoded = PngTestDecoder.Decode(stream.ToArray());
        Assert.Equal(image.Pixels.ToArray(), decoded.Pixels);
        Assert.Equal(2, decoded.ColorType);
    }

    [Fact]
    public void WritesOnePixel()
    {
        PngTestImage decoded = PngTestDecoder.Decode(Encode(1, 1, [7, 8, 9, 10], PngPixelFormat.Rgba32));

        Assert.Equal([7, 8, 9, 10], decoded.Pixels);
    }

    [Fact]
    public void SplitsLargeImageDataIntoSeveralChunks()
    {
        // Noise doesn't compress, so 1.5 MB of it needs two data chunks of at most 1 MiB.
        var random = new Random(42);
        byte[] pixels = new byte[1000 * 500 * 3];
        random.NextBytes(pixels);

        PngTestImage decoded = PngTestDecoder.Decode(Encode(1000, 500, pixels, PngPixelFormat.Rgb24));

        Assert.Equal(2, decoded.DataChunks);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void WritesTheSameBytesEveryTime()
    {
        byte[] pixels = TestPattern(50, 40, 3);

        Assert.Equal(Encode(50, 40, pixels, PngPixelFormat.Rgb24), Encode(50, 40, pixels, PngPixelFormat.Rgb24));
    }

    [Fact]
    public void RejectsInconsistentInput()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Write(stream, 0, 1, [], PngPixelFormat.Gray8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Write(stream, 1, 0, [], PngPixelFormat.Gray8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Write(stream, 1, 1, [0], (PngPixelFormat)99));
        ArgumentException wrongSize = Assert.Throws<ArgumentException>(() => PngEncoder.Write(stream, 2, 2, new byte[11], PngPixelFormat.Rgb24));
        Assert.Contains("A 2 × 2 Rgb24 image has 12 bytes, not 11.", wrongSize.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => PngEncoder.Write(null!, 1, 1, [0], PngPixelFormat.Gray8));
        Assert.Throws<ArgumentNullException>(() => PngEncoder.Write(stream, null!));
        Assert.Equal(0, stream.Length);
    }

    private static byte[] Encode(int width, int height, byte[] pixels, PngPixelFormat format)
    {
        using var stream = new MemoryStream();
        PngEncoder.Write(stream, width, height, pixels, format);
        return stream.ToArray();
    }

    // Rows of different character: flat, horizontal and vertical gradients, a checkerboard and noise.
    private static byte[] TestPattern(int width, int height, int channels)
    {
        var random = new Random(width * height);
        byte[] pixels = new byte[width * height * channels];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int c = 0; c < channels; c++)
                {
                    pixels[(((y * width) + x) * channels) + c] = (y % 5) switch
                    {
                        0 => (byte)(40 + c),
                        1 => (byte)((x * 7) + c),
                        2 => (byte)((y * 11) + (c * 3)),
                        3 => (byte)(((x + y) % 2) * 255),
                        _ => (byte)random.Next(256),
                    };
                }
            }
        }

        return pixels;
    }
}
