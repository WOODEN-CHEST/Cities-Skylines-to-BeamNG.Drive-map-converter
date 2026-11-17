using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace CsToBeamNG.Converter.Imaging;

/// <summary>
/// Writes PNG images as the PNG specification (W3C, third edition) defines them: 8 bits per channel, no
/// interlacing, adaptive row filters, and the image data compressed with the zlib of .NET's
/// System.IO.Compression.
/// </summary>
/// <remarks>
/// The pixels always decode exactly as given. The compressed bytes depend on the zlib inside the .NET runtime, so
/// an image gives the same file with the same runtime, but not necessarily with another one; compare decoded
/// pixels, not files.
/// </remarks>
public static class PngEncoder
{
    // Image data is split into chunks of at most this size. Any size is valid; decoders join them into one stream.
    private const int MaxDataChunkBytes = 1 << 20;

    private const int FilterTypeCount = 5;

    // The bytes every PNG file starts with: 0x89, "PNG", CR LF, Ctrl-Z, LF.
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Writes an RGB image.</summary>
    /// <param name="stream">Where to write the file's bytes.</param>
    /// <param name="image">The image.</param>
    public static void Write(Stream stream, RgbImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Write(stream, image.Width, image.Height, image.Pixels, PngPixelFormat.Rgb24);
    }

    /// <summary>Writes an image from raw pixels.</summary>
    /// <param name="stream">Where to write the file's bytes.</param>
    /// <param name="width">The width in pixels, 1 or more.</param>
    /// <param name="height">The height in pixels, 1 or more.</param>
    /// <param name="pixels">The pixels, row by row from the top, in <paramref name="format"/>, without padding.</param>
    /// <param name="format">The layout of each pixel.</param>
    /// <exception cref="ArgumentException">The number of bytes doesn't match the size and format.</exception>
    public static void Write(Stream stream, int width, int height, ReadOnlySpan<byte> pixels, PngPixelFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        (int channels, byte colorType) = format switch
        {
            PngPixelFormat.Gray8 => (1, (byte)0),
            PngPixelFormat.Rgb24 => (3, (byte)2),
            PngPixelFormat.Rgba32 => (4, (byte)6),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format."),
        };
        long rowBytes = (long)width * channels;
        if (pixels.Length != rowBytes * height)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"A {width} × {height} {format} image has {rowBytes * height} bytes, not {pixels.Length}."),
                nameof(pixels));
        }

        stream.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)height);
        header[8] = 8; // bits per channel
        header[9] = colorType;
        header[10] = 0; // compression method: zlib deflate
        header[11] = 0; // filter method: adaptive, five filter types
        header[12] = 0; // interlace method: none
        WriteChunk(stream, "IHDR"u8, header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                WriteFilteredRows(zlib, pixels, (int)rowBytes, channels);
            }

            ReadOnlySpan<byte> data = compressed.GetBuffer().AsSpan(0, (int)compressed.Length);
            for (int offset = 0; offset < data.Length; offset += MaxDataChunkBytes)
            {
                WriteChunk(stream, "IDAT"u8, data.Slice(offset, Math.Min(MaxDataChunkBytes, data.Length - offset)));
            }
        }

        WriteChunk(stream, "IEND"u8, []);
    }

    // Writes each row as its filter type and the filtered bytes. All five filters are tried, and the one whose
    // output has the smallest sum of absolute values, read as signed bytes, is kept: the heuristic the PNG
    // specification recommends (section 12.8, "Filter selection"). Ties go to the lower filter type, so the search
    // stops at a filter whose output is all zeros.
    private static void WriteFilteredRows(Stream output, ReadOnlySpan<byte> pixels, int rowBytes, int bytesPerPixel)
    {
        byte[][] candidates = new byte[FilterTypeCount][];
        for (int type = 0; type < FilterTypeCount; type++)
        {
            candidates[type] = new byte[rowBytes + 1];
            candidates[type][0] = (byte)type;
        }

        // The row above the first one counts as all zeros.
        byte[] above = new byte[rowBytes];
        for (int start = 0; start < pixels.Length; start += rowBytes)
        {
            ReadOnlySpan<byte> row = pixels.Slice(start, rowBytes);
            int best = 0;
            long bestSum = long.MaxValue;
            for (int type = 0; type < FilterTypeCount && bestSum > 0; type++)
            {
                long sum = Filter(type, row, above, bytesPerPixel, candidates[type].AsSpan(1));
                if (sum < bestSum)
                {
                    best = type;
                    bestSum = sum;
                }
            }

            output.Write(candidates[best]);
            row.CopyTo(above);
        }
    }

    // The filter types of the PNG specification, section 9.2: each byte minus a prediction from the byte one pixel
    // to the left (a), the byte above (b) and the byte above and to the left (c); a and c are 0 in the first pixel.
    // Returns the sum of the output's absolute values as signed bytes. One loop per type keeps the inner loops simple.
    private static long Filter(int type, ReadOnlySpan<byte> row, ReadOnlySpan<byte> above, int bytesPerPixel, Span<byte> output)
    {
        long sum = 0;
        int first = Math.Min(bytesPerPixel, row.Length);
        switch (type)
        {
            case 0:
                for (int i = 0; i < row.Length; i++)
                {
                    sum += Store(output, i, row[i]);
                }

                break;
            case 1:
                for (int i = 0; i < first; i++)
                {
                    sum += Store(output, i, row[i]);
                }

                for (int i = first; i < row.Length; i++)
                {
                    sum += Store(output, i, row[i] - row[i - bytesPerPixel]);
                }

                break;
            case 2:
                for (int i = 0; i < row.Length; i++)
                {
                    sum += Store(output, i, row[i] - above[i]);
                }

                break;
            case 3:
                for (int i = 0; i < first; i++)
                {
                    sum += Store(output, i, row[i] - (above[i] >> 1));
                }

                for (int i = first; i < row.Length; i++)
                {
                    sum += Store(output, i, row[i] - ((row[i - bytesPerPixel] + above[i]) >> 1));
                }

                break;
            default:
                for (int i = 0; i < first; i++)
                {
                    sum += Store(output, i, row[i] - Paeth(0, above[i], 0));
                }

                for (int i = first; i < row.Length; i++)
                {
                    sum += Store(output, i, row[i] - Paeth(row[i - bytesPerPixel], above[i], above[i - bytesPerPixel]));
                }

                break;
        }

        return sum;
    }

    // Stores a filtered byte (the difference modulo 256) and returns its absolute value as a signed byte.
    private static int Store(Span<byte> output, int index, int difference)
    {
        byte value = (byte)difference;
        output[index] = value;
        return Math.Abs((int)(sbyte)value);
    }

    private static int Paeth(int a, int b, int c)
    {
        int estimate = a + b - c;
        int distanceA = Math.Abs(estimate - a);
        int distanceB = Math.Abs(estimate - b);
        int distanceC = Math.Abs(estimate - c);
        if (distanceA <= distanceB && distanceA <= distanceC)
        {
            return a;
        }

        return distanceB <= distanceC ? b : c;
    }

    // A chunk: the data's length, the four-letter type, the data, and the CRC of type and data; numbers big-endian.
    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        stream.Write(number);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32.Append(Crc32.Compute(type), data));
        stream.Write(number);
    }
}
