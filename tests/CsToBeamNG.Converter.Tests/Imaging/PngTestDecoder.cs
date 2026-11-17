using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Imaging;

/// <summary>
/// A small PNG decoder for tests, written independently of the encoder: it checks the signature and every chunk's
/// CRC (bit by bit, without a table), joins the image data, inflates it and undoes the row filters.
/// </summary>
internal static class PngTestDecoder
{
    public static PngTestImage Decode(byte[] file)
    {
        byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(signature, file[..8]);
        var chunks = new List<(string Type, byte[] Data)>();
        int position = 8;
        while (position < file.Length)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position)));
            byte[] typeAndData = file[(position + 4)..(position + 8 + length)];
            uint crc = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position + 8 + length));
            Assert.Equal(BitwiseCrc32(typeAndData), crc);
            chunks.Add((System.Text.Encoding.ASCII.GetString(typeAndData, 0, 4), typeAndData[4..]));
            position += 12 + length;
        }

        Assert.Equal("IHDR", chunks[0].Type);
        Assert.Equal("IEND", chunks[^1].Type);
        Assert.Empty(chunks[^1].Data);
        byte[] header = chunks[0].Data;
        Assert.Equal(13, header.Length);
        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(header);
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
        Assert.Equal(8, header[8]);
        Assert.Equal([0, 0, 0], header[10..]);
        int channels = header[9] switch
        {
            0 => 1,
            2 => 3,
            6 => 4,
            _ => throw new InvalidDataException("Unexpected colour type " + header[9]),
        };

        var compressed = new MemoryStream();
        int dataChunks = 0;
        foreach ((string type, byte[] data) in chunks)
        {
            if (type == "IDAT")
            {
                compressed.Write(data);
                dataChunks++;
            }
        }

        compressed.Position = 0;
        var filtered = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            zlib.CopyTo(filtered);
        }

        int rowBytes = width * channels;
        byte[] raw = filtered.ToArray();
        Assert.Equal((rowBytes + 1) * height, raw.Length);
        byte[] pixels = new byte[rowBytes * height];
        var filtersUsed = new SortedSet<int>();
        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (rowBytes + 1)];
            filtersUsed.Add(filter);
            for (int i = 0; i < rowBytes; i++)
            {
                int a = i >= channels ? pixels[(y * rowBytes) + i - channels] : 0;
                int b = y > 0 ? pixels[((y - 1) * rowBytes) + i] : 0;
                int c = i >= channels && y > 0 ? pixels[((y - 1) * rowBytes) + i - channels] : 0;
                int prediction = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException("Unknown filter " + filter),
                };
                pixels[(y * rowBytes) + i] = (byte)(raw[(y * (rowBytes + 1)) + 1 + i] + prediction);
            }
        }

        return new PngTestImage(width, height, header[9], pixels, dataChunks, filtersUsed);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static uint BitwiseCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) * 0xEDB88320);
            }
        }

        return ~crc;
    }
}

/// <summary>A decoded PNG image.</summary>
internal sealed record PngTestImage(int Width, int Height, int ColorType, byte[] Pixels, int DataChunks, IReadOnlySet<int> FiltersUsed);
