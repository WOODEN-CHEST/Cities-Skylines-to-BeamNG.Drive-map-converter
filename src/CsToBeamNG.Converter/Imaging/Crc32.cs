using System;

namespace CsToBeamNG.Converter.Imaging;

/// <summary>
/// The CRC-32 that PNG chunks carry (ISO 3309, polynomial 0x04C11DB7 in its reflected form 0xEDB88320), as defined
/// in the PNG specification, annex D. Its check value, the CRC of the ASCII text <c>123456789</c>, is 0xCBF43926.
/// </summary>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320;

    // A lookup table of the CRC of every byte value, computed once and never changed.
    private static readonly uint[] _table = CreateTable();

    /// <summary>Computes the CRC of some bytes.</summary>
    /// <param name="data">The bytes.</param>
    public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);

    /// <summary>Continues a CRC with more bytes: Append(Compute(a), b) equals the CRC of a followed by b.</summary>
    /// <param name="crc">The CRC of the bytes so far, 0 for none.</param>
    /// <param name="data">The next bytes.</param>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        uint register = ~crc;
        foreach (byte value in data)
        {
            register = _table[(register ^ value) & 0xFF] ^ (register >> 8);
        }

        return ~register;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            uint c = n;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
