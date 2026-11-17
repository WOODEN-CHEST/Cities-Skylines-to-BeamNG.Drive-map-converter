using System;
using System.Globalization;
using System.IO;
#if NET
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

namespace CsToBeamNG.Dump.Records;

/// <summary>
/// Reads a record file written by <see cref="RecordFileWriter"/> (docs/dump-format.md, Record files).
/// </summary>
/// <remarks>
/// <para>
/// The constructor reads and checks the header; a damaged, unfinished or unsupported file throws a
/// <see cref="DumpException"/> that names it. Then <see cref="MoveNext"/> steps through the records, and
/// the field methods read the current record in the order of its layout.
/// </para>
/// <para>
/// Records of a newer minor version can have more fields at the end. The reader always advances by the
/// file's own <see cref="RecordSize"/>, so fields that a caller doesn't read are skipped. To read a field
/// that a newer minor version appended, check that <see cref="RecordSize"/> covers it first.
/// </para>
/// <para>Not thread-safe.</para>
/// </remarks>
public sealed class RecordFileReader : IDisposable
{
    private const int TargetBufferSize = 64 * 1024;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _buffer;
    private int _bufferedRecords;
    private int _bufferIndex;
    private int _fieldPosition;
    private int _recordEnd;
    private bool _hasRecord;
    private long _loadedRecords;
    private long _recordIndex = -1;
    private bool _disposed;

    /// <summary>Reads and checks the header of a record file.</summary>
    /// <param name="stream">A readable stream, positioned where the file starts.</param>
    /// <param name="sourceName">The file's name or path, for messages.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when the reader is disposed.</param>
    /// <exception cref="DumpException">The header is damaged or unsupported, or the file was never finished.</exception>
    public RecordFileReader(Stream stream, string sourceName, bool leaveOpen)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(sourceName, nameof(sourceName));
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        SourceName = sourceName;
        long start = stream.CanSeek ? stream.Position : 0;

        byte[] header = new byte[RecordFileFormat.HeaderSize];
        int headerBytes = ReadFully(header, header.Length);
        CheckMagic(header, headerBytes);
        if (headerBytes < header.Length)
        {
            throw Error(DumpErrorKind.Corrupt, "is truncated: it ends inside its header");
        }

        int version = LittleEndian.ReadUInt16(header, RecordFileFormat.VersionOffset);
        if (version != RecordFileFormat.ContainerVersion)
        {
            throw Error(
                DumpErrorKind.Incompatible,
                Format("has record file version {0}, but this program reads version {1}", version, RecordFileFormat.ContainerVersion));
        }

        int headerSize = LittleEndian.ReadUInt16(header, RecordFileFormat.HeaderSizeOffset);
        if (headerSize != RecordFileFormat.HeaderSize)
        {
            throw Error(DumpErrorKind.Corrupt, Format("has a header size of {0} bytes instead of {1}", headerSize, RecordFileFormat.HeaderSize));
        }

        uint flags = LittleEndian.ReadUInt32(header, RecordFileFormat.FlagsOffset);
        if (flags != 0)
        {
            throw Error(
                DumpErrorKind.Incompatible,
                "uses features that this program doesn't support (flags 0x" + flags.ToString("X8", CultureInfo.InvariantCulture) + ")");
        }

        uint recordSize = LittleEndian.ReadUInt32(header, RecordFileFormat.RecordSizeOffset);
        if (recordSize < 1 || recordSize > RecordFileFormat.MaxRecordSize)
        {
            throw Error(DumpErrorKind.Corrupt, Format("has an invalid record size of {0} bytes", recordSize));
        }

        ulong count = LittleEndian.ReadUInt64(header, RecordFileFormat.RecordCountOffset);
        if (count == RecordFileFormat.UnfinishedRecordCount)
        {
            throw Error(
                DumpErrorKind.Incomplete,
                "was never finished: the export failed or was interrupted while writing it. Export the city again");
        }

        if (count > (ulong)((long.MaxValue - RecordFileFormat.HeaderSize) / recordSize))
        {
            throw Error(DumpErrorKind.Corrupt, Format("has an impossible record count of {0}", count));
        }

        RecordType = ReadRecordType(header)
            ?? throw Error(DumpErrorKind.Corrupt, "has an invalid record type name in its header");
        RecordSize = (int)recordSize;
        RecordCount = (long)count;

        if (stream.CanSeek)
        {
            long expected = RecordFileFormat.HeaderSize + (RecordCount * RecordSize);
            long actual = stream.Length - start;
            if (actual < expected)
            {
                throw Error(
                    DumpErrorKind.Corrupt,
                    Format("is truncated: it has {0} bytes, but its header announces {1} records of {2} bytes ({3} bytes in all)", actual, RecordCount, RecordSize, expected));
            }

            if (actual > expected)
            {
                throw Error(DumpErrorKind.Corrupt, Format("has {0} unexpected bytes after its last record", actual - expected));
            }
        }

        long bufferRecords = Math.Min(Math.Max(1, TargetBufferSize / RecordSize), Math.Max(1, RecordCount));
        _buffer = new byte[bufferRecords * RecordSize];
    }

    /// <summary>The file's name or path, as used in messages.</summary>
    public string SourceName { get; }

    /// <summary>The record type name from the header.</summary>
    public string RecordType { get; }

    /// <summary>The size of each record in bytes, from the header. Records advance by this size.</summary>
    public int RecordSize { get; }

    /// <summary>The number of records in the file.</summary>
    public long RecordCount { get; }

    /// <summary>The index of the current record: -1 before the first <see cref="MoveNext"/>, <see cref="RecordCount"/> after the last.</summary>
    public long RecordIndex => _recordIndex;

    /// <summary>The number of bytes of the current record that haven't been read.</summary>
    public int RemainingInRecord => _hasRecord ? _recordEnd - _fieldPosition : 0;

    /// <summary>Opens a record file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="DumpException">The file doesn't exist, or its header is damaged or unsupported.</exception>
    public static RecordFileReader Open(string path)
    {
        Guard.NotNull(path, nameof(path));
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException e)
        {
            throw new DumpException(DumpErrorKind.NotFound, Format("The record file '{0}' doesn't exist.", path), e);
        }
        catch (DirectoryNotFoundException e)
        {
            throw new DumpException(DumpErrorKind.NotFound, Format("The record file '{0}' doesn't exist.", path), e);
        }

        try
        {
            return new RecordFileReader(stream, path, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Opens a record file and checks that it holds the expected records.</summary>
    /// <param name="path">The file.</param>
    /// <param name="recordType">The record type the file must hold.</param>
    /// <param name="minimumRecordSize">The size of the layout's first version, which later minor versions only extend.</param>
    /// <exception cref="DumpException">The file doesn't exist, is damaged or unsupported, or holds other records.</exception>
    public static RecordFileReader Open(string path, string recordType, int minimumRecordSize)
    {
        RecordFileReader reader = Open(path);
        try
        {
            reader.CheckLayout(recordType, minimumRecordSize);
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    /// <summary>Checks that the file holds the expected records.</summary>
    /// <param name="recordType">The record type the file must hold.</param>
    /// <param name="minimumRecordSize">The size of the layout's first version, which later minor versions only extend.</param>
    /// <exception cref="DumpException">The file holds other records, or records that are too small.</exception>
    public void CheckLayout(string recordType, int minimumRecordSize)
    {
        Guard.NotNull(recordType, nameof(recordType));
        if (!string.Equals(RecordType, recordType, StringComparison.Ordinal))
        {
            throw Error(DumpErrorKind.Corrupt, Format("holds '{0}' records instead of '{1}' records", RecordType, recordType));
        }

        if (RecordSize < minimumRecordSize)
        {
            throw Error(
                DumpErrorKind.Corrupt,
                Format("has {0}-byte '{1}' records, but they have at least {2} bytes", RecordSize, RecordType, minimumRecordSize));
        }
    }

    /// <summary>Moves to the next record.</summary>
    /// <returns><see langword="false"/> when there are no more records.</returns>
    /// <exception cref="DumpException">The file ends early (a stream whose length can't be checked up front).</exception>
    public bool MoveNext()
    {
        Guard.NotDisposed(_disposed, this);
        if (_recordIndex + 1 >= RecordCount)
        {
            _recordIndex = RecordCount;
            _hasRecord = false;
            return false;
        }

        if (!_hasRecord || _bufferIndex + 1 >= _bufferedRecords)
        {
            Fill();
        }
        else
        {
            _bufferIndex++;
        }

        _recordIndex++;
        _hasRecord = true;
        _fieldPosition = _bufferIndex * RecordSize;
        _recordEnd = _fieldPosition + RecordSize;
        return true;
    }

    /// <summary>Reads a <c>u8</c> field.</summary>
    public byte ReadByte() => _buffer[Take(1)];

    /// <summary>Reads an <c>i8</c> field.</summary>
    public sbyte ReadSByte() => (sbyte)_buffer[Take(1)];

    /// <summary>Reads a <c>u16</c> field.</summary>
    public ushort ReadUInt16() => LittleEndian.ReadUInt16(_buffer, Take(2));

    /// <summary>Reads an <c>i16</c> field.</summary>
    public short ReadInt16() => (short)LittleEndian.ReadUInt16(_buffer, Take(2));

    /// <summary>Reads a <c>u32</c> field, or a <c>str</c> field (a string table index).</summary>
    public uint ReadUInt32() => LittleEndian.ReadUInt32(_buffer, Take(4));

    /// <summary>Reads an <c>i32</c> field.</summary>
    public int ReadInt32() => (int)LittleEndian.ReadUInt32(_buffer, Take(4));

    /// <summary>Reads a <c>u64</c> field.</summary>
    public ulong ReadUInt64() => LittleEndian.ReadUInt64(_buffer, Take(8));

    /// <summary>Reads an <c>i64</c> field.</summary>
    public long ReadInt64() => (long)LittleEndian.ReadUInt64(_buffer, Take(8));

    /// <summary>Reads an <c>f32</c> field.</summary>
    public float ReadSingle() => FloatBits.FromBits(LittleEndian.ReadUInt32(_buffer, Take(4)));

    /// <summary>Reads an <c>f64</c> field.</summary>
    public double ReadDouble() => BitConverter.Int64BitsToDouble((long)LittleEndian.ReadUInt64(_buffer, Take(8)));

    /// <summary>Copies bytes of the current record, such as a fixed-size byte array field.</summary>
    /// <param name="destination">The array to copy into.</param>
    /// <param name="offset">The index in <paramref name="destination"/> to copy to.</param>
    /// <param name="count">The number of bytes.</param>
    public void ReadBytes(byte[] destination, int offset, int count)
    {
        Guard.NotNull(destination, nameof(destination));
        if (offset < 0 || count < 0 || count > destination.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The range lies outside the destination array.");
        }

        Buffer.BlockCopy(_buffer, Take(count), destination, offset, count);
    }

    /// <summary>Skips bytes of the current record: padding, reserved or unneeded fields.</summary>
    /// <param name="count">The number of bytes.</param>
    public void Skip(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count can't be negative.");
        }

        Take(count);
    }

#if NET
    /// <summary>
    /// Reads all records at once into structs whose memory layout matches the start of the record layout:
    /// sequential, packed, little-endian fields. Use it for large arrays of simple records, such as height maps.
    /// </summary>
    /// <typeparam name="T">
    /// An unmanaged struct no larger than <see cref="RecordSize"/>. If the records are larger, as with a newer
    /// minor version, the bytes beyond the struct are skipped.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// Records have been read already, or <typeparamref name="T"/> is larger than <see cref="RecordSize"/>.
    /// </exception>
    /// <exception cref="DumpException">The file ends early.</exception>
    public T[] ReadAllRecords<T>()
        where T : unmanaged
    {
        Guard.NotDisposed(_disposed, this);
        if (_recordIndex != -1)
        {
            throw new InvalidOperationException("ReadAllRecords reads the whole file; call it before MoveNext.");
        }

        int size = Unsafe.SizeOf<T>();
        if (size > RecordSize)
        {
            throw new InvalidOperationException(Format(
                "{0} has {1} bytes, but the '{2}' records in '{3}' have only {4}.", typeof(T).Name, size, RecordType, SourceName, RecordSize));
        }

        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("ReadAllRecords needs a little-endian machine.");
        }

        if (RecordCount > Array.MaxLength)
        {
            throw Error(DumpErrorKind.Corrupt, Format("has more records ({0}) than an array can hold", RecordCount));
        }

        var records = new T[RecordCount];
        Span<byte> destination = MemoryMarshal.AsBytes(records.AsSpan());
        if (size == RecordSize)
        {
            int read = _stream.ReadAtLeast(destination, destination.Length, throwOnEndOfStream: false);
            if (read < destination.Length)
            {
                throw Truncated(read / RecordSize);
            }

            _loadedRecords = RecordCount;
        }
        else
        {
            for (long i = 0; i < RecordCount; i++)
            {
                MoveNext();
                _buffer.AsSpan(_fieldPosition, size).CopyTo(destination.Slice((int)(i * size), size));
            }
        }

        _recordIndex = RecordCount;
        _hasRecord = false;
        return records;
    }
#endif

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    private static string? ReadRecordType(byte[] header)
    {
        int start = RecordFileFormat.RecordTypeOffset;
        int length = 0;
        while (length < RecordFileFormat.MaxRecordTypeLength && header[start + length] != 0)
        {
            length++;
        }

        // The rest of the field must be zero padding.
        for (int i = start + length; i < start + RecordFileFormat.MaxRecordTypeLength; i++)
        {
            if (header[i] != 0)
            {
                return null;
            }
        }

        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            byte b = header[start + i];
            if (b > 127)
            {
                return null;
            }

            chars[i] = (char)b;
        }

        string name = new string(chars);
        return DumpNames.IsValidRecordType(name) ? name : null;
    }

    private void CheckMagic(byte[] header, int headerBytes)
    {
        bool matches = headerBytes >= RecordFileFormat.Magic.Length;
        for (int i = 0; matches && i < RecordFileFormat.Magic.Length; i++)
        {
            matches = header[i] == RecordFileFormat.Magic[i];
        }

        if (matches)
        {
            return;
        }

        if (headerBytes < RecordFileFormat.Magic.Length)
        {
            throw Error(DumpErrorKind.Corrupt, Format("is not a CsToBeamNG record file: it has only {0} bytes", headerBytes));
        }

        string start = BitConverter.ToString(header, 0, RecordFileFormat.Magic.Length).Replace('-', ' ');
        throw Error(
            DumpErrorKind.Corrupt,
            Format("is not a CsToBeamNG record file: it starts with the bytes {0} instead of '{1}'", start, RecordFileFormat.Magic));
    }

    private void Fill()
    {
        long remaining = RecordCount - _loadedRecords;
        int records = (int)Math.Min(remaining, _buffer.Length / RecordSize);
        int bytes = records * RecordSize;
        int read = ReadFully(_buffer, bytes);
        if (read < bytes)
        {
            throw Truncated(_loadedRecords + (read / RecordSize));
        }

        _loadedRecords += records;
        _bufferedRecords = records;
        _bufferIndex = 0;
    }

    private int Take(int size)
    {
        Guard.NotDisposed(_disposed, this);
        if (!_hasRecord)
        {
            throw new InvalidOperationException("There is no current record; call MoveNext first.");
        }

        int offset = _fieldPosition;
        if (offset + size > _recordEnd)
        {
            throw new InvalidOperationException(Format(
                "Reading {0} bytes at offset {1} would pass the end of the {2}-byte '{3}' record. Check RecordSize before reading fields that newer layouts append.",
                size,
                offset - (_recordEnd - RecordSize),
                RecordSize,
                RecordType));
        }

        _fieldPosition = offset + size;
        return offset;
    }

    private int ReadFully(byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = _stream.Read(buffer, total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private DumpException Truncated(long completeRecords) =>
        Error(DumpErrorKind.Corrupt, Format("is truncated: it ends after {0} of {1} records", completeRecords, RecordCount));

    private DumpException Error(DumpErrorKind kind, string problem) =>
        new(kind, "'" + SourceName + "' " + problem + ".");
}
