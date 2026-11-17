namespace CsToBeamNG.Dump.Records;

/// <summary>
/// Little-endian encoding into byte arrays, independent of the machine's byte order and without
/// allocations. It is the same code on .NET 3.5, which lacks BinaryPrimitives, and on .NET 10, so the
/// tests cover what runs in the game.
/// </summary>
internal static class LittleEndian
{
    public static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    public static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    public static void WriteUInt64(byte[] buffer, int offset, ulong value)
    {
        WriteUInt32(buffer, offset, (uint)value);
        WriteUInt32(buffer, offset + 4, (uint)(value >> 32));
    }

    public static ushort ReadUInt16(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    public static uint ReadUInt32(byte[] buffer, int offset) =>
        buffer[offset]
        | ((uint)buffer[offset + 1] << 8)
        | ((uint)buffer[offset + 2] << 16)
        | ((uint)buffer[offset + 3] << 24);

    public static ulong ReadUInt64(byte[] buffer, int offset) =>
        ReadUInt32(buffer, offset) | ((ulong)ReadUInt32(buffer, offset + 4) << 32);
}
