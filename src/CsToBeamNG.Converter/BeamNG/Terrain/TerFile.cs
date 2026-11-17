using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// Reads and writes <c>.ter</c> files, version 9 (docs/research/beamng-level-format.md, Terrain): a version
/// byte, the size as a 32-bit integer, Size² 16-bit heights, Size² material bytes, the material count as a
/// 32-bit integer, and each material name as a length byte followed by ASCII. Everything is little-endian.
/// </summary>
public static class TerFile
{
    /// <summary>The format version this code reads and writes.</summary>
    public const byte Version = 9;

    /// <summary>The length of a file for the given terrain.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>The length in bytes.</returns>
    public static long GetLength(TerrainData terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        long samples = (long)terrain.Size * terrain.Size;
        long length = 1 + 4 + (samples * 3) + 4;
        foreach (string name in terrain.MaterialNames)
        {
            length += 1 + name.Length;
        }

        return length;
    }

    /// <summary>Writes a terrain.</summary>
    /// <param name="stream">The destination, left open.</param>
    /// <param name="terrain">The terrain.</param>
    public static void Write(Stream stream, TerrainData terrain)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(terrain);

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Version);
        writer.Write(terrain.Size);
        writer.Flush();
        WriteHeights(stream, terrain.Heights);
        stream.Write(terrain.Materials);
        writer.Write(terrain.MaterialNames.Count);
        foreach (string name in terrain.MaterialNames)
        {
            byte[] bytes = TerrainData.EncodeMaterialName(name);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
    }

    /// <summary>Reads a terrain.</summary>
    /// <param name="stream">The source, left open.</param>
    /// <returns>The terrain.</returns>
    /// <exception cref="InvalidDataException">The data isn't a version 9 <c>.ter</c> file.</exception>
    public static TerrainData Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            byte version = reader.ReadByte();
            if (version != Version)
            {
                throw new InvalidDataException($"Unsupported .ter version {version}; expected {Version}.");
            }

            int size = reader.ReadInt32();
            if (size < 2 || size > TerrainData.MaxSize || !BitOperations.IsPow2(size))
            {
                throw new InvalidDataException($"Invalid .ter size {size}.");
            }

            byte[] heightBytes = reader.ReadBytes(size * size * sizeof(ushort));
            byte[] materials = reader.ReadBytes(size * size);
            int count = reader.ReadInt32();
            if (heightBytes.Length != size * size * sizeof(ushort) || materials.Length != size * size || count is < 1 or > TerrainData.MaxMaterialCount)
            {
                throw new InvalidDataException("The .ter file is truncated or has an invalid material count.");
            }

            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                byte length = reader.ReadByte();
                byte[] name = reader.ReadBytes(length);
                if (name.Length != length)
                {
                    throw new InvalidDataException("The .ter file is truncated in the material names.");
                }

                names[i] = Encoding.ASCII.GetString(name);
            }

            var terrain = new TerrainData(size, names);
            Span<ushort> heights = terrain.Heights;
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = BinaryPrimitives.ReadUInt16LittleEndian(heightBytes.AsSpan(i * sizeof(ushort)));
            }

            materials.CopyTo(terrain.Materials);
            return terrain;
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("The .ter file is truncated.", e);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"The .ter file is invalid: {e.Message}", e);
        }
    }

    private static void WriteHeights(Stream stream, ReadOnlySpan<ushort> heights)
    {
        if (BitConverter.IsLittleEndian)
        {
            stream.Write(MemoryMarshal.AsBytes(heights));
            return;
        }

        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        foreach (ushort height in heights)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, height);
            stream.Write(buffer);
        }
    }
}
