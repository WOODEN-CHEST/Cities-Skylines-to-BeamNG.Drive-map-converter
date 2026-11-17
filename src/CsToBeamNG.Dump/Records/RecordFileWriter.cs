using System;
using System.Globalization;
using System.IO;

namespace CsToBeamNG.Dump.Records;

/// <summary>
/// Writes a record file: a header, then fixed-size records of little-endian fields
/// (docs/dump-format.md, Record files).
/// </summary>
/// <remarks>
/// <para>
/// Write each record between <see cref="BeginRecord"/> and <see cref="EndRecord"/>, field by field in the
/// order of its layout. <see cref="EndRecord"/> checks that exactly <see cref="RecordSize"/> bytes were
/// written, so a layout mistake fails at once instead of producing a misaligned file. If computing a
/// record's values fails halfway, <see cref="DiscardRecord"/> drops the record and the file stays valid.
/// </para>
/// <para>
/// The header holds <see cref="RecordFileFormat.UnfinishedRecordCount"/> until <see cref="Finish"/> writes
/// the real count, so readers reject a file whose writing failed or was interrupted. Disposing without
/// finishing keeps the records written so far, for diagnosis, but the file stays unfinished.
/// </para>
/// <para>Not thread-safe. It runs on .NET 3.5 in the game and doesn't allocate per record or field.</para>
/// </remarks>
public sealed class RecordFileWriter : IDisposable
{
    private const int NoRecord = -1;
    private const int MinBufferSize = 64 * 1024;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly long _headerPosition;
    private readonly byte[] _buffer;
    private int _position;
    private int _recordStart = NoRecord;
    private long _recordCount;
    private bool _finished;
    private bool _disposed;

    /// <summary>Starts a record file on a stream and writes its header.</summary>
    /// <param name="stream">
    /// A writable, seekable stream, positioned where the file starts. The writer seeks back to write the
    /// record count into the header when the file is finished.
    /// </param>
    /// <param name="recordType">The record type name, such as <c>netNode</c> (docs/dump-format.md, Names).</param>
    /// <param name="recordSize">The size of each record in bytes, 1 to <see cref="RecordFileFormat.MaxRecordSize"/>.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when the writer is disposed.</param>
    public RecordFileWriter(Stream stream, string recordType, int recordSize, bool leaveOpen)
    {
        Guard.NotNull(stream, nameof(stream));
        if (!stream.CanWrite || !stream.CanSeek)
        {
            throw new ArgumentException(
                "The stream must be writable and seekable: the record count is written into the header when the file is finished.",
                nameof(stream));
        }

        ValidateLayout(recordType, recordSize);
        _stream = stream;
        _leaveOpen = leaveOpen;
        RecordType = recordType;
        RecordSize = recordSize;
        _headerPosition = stream.Position;
        _buffer = new byte[Math.Max(MinBufferSize, recordSize)];
        WriteHeader();
    }

    /// <summary>The record type name in the header.</summary>
    public string RecordType { get; }

    /// <summary>The size of each record in bytes.</summary>
    public int RecordSize { get; }

    /// <summary>The number of records ended so far.</summary>
    public long RecordCount => _recordCount;

    /// <summary>Whether <see cref="Finish"/> has written the record count, which completes the file.</summary>
    public bool IsFinished => _finished;

    internal bool IsDisposed => _disposed;

    /// <summary>Creates a new file and starts a record file in it.</summary>
    /// <param name="path">The file to create. It must not exist.</param>
    /// <param name="recordType">The record type name, such as <c>netNode</c> (docs/dump-format.md, Names).</param>
    /// <param name="recordSize">The size of each record in bytes, 1 to <see cref="RecordFileFormat.MaxRecordSize"/>.</param>
    /// <exception cref="IOException">The file exists or can't be created.</exception>
    public static RecordFileWriter Create(string path, string recordType, int recordSize)
    {
        Guard.NotNull(path, nameof(path));
        ValidateLayout(recordType, recordSize);
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            return new RecordFileWriter(stream, recordType, recordSize, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Starts the next record.</summary>
    /// <exception cref="InvalidOperationException">A record is in progress, or the file is finished.</exception>
    public void BeginRecord()
    {
        ThrowIfClosed();
        if (_recordStart != NoRecord)
        {
            throw new InvalidOperationException("The previous record isn't ended; call EndRecord or DiscardRecord first.");
        }

        // Flush only between records, so that the current record is always in the buffer and can be discarded.
        if (_position + RecordSize > _buffer.Length)
        {
            FlushBuffer();
        }

        _recordStart = _position;
    }

    /// <summary>Ends the current record.</summary>
    /// <exception cref="InvalidOperationException">
    /// No record is in progress, or fewer than <see cref="RecordSize"/> bytes were written. The record stays
    /// in progress, so it can still be discarded.
    /// </exception>
    public void EndRecord()
    {
        ThrowIfNoRecord();
        int written = _position - _recordStart;
        if (written != RecordSize)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "A '{0}' record has {1} bytes, but only {2} were written.",
                RecordType,
                RecordSize,
                written));
        }

        _recordStart = NoRecord;
        _recordCount++;
    }

    /// <summary>Drops the current record, as if it had never been begun.</summary>
    /// <exception cref="InvalidOperationException">No record is in progress.</exception>
    public void DiscardRecord()
    {
        ThrowIfNoRecord();
        _position = _recordStart;
        _recordStart = NoRecord;
    }

    /// <summary>Writes a <c>u8</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteByte(byte value) => _buffer[Reserve(1)] = value;

    /// <summary>Writes an <c>i8</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteSByte(sbyte value) => _buffer[Reserve(1)] = (byte)value;

    /// <summary>Writes a <c>u16</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteUInt16(ushort value) => LittleEndian.WriteUInt16(_buffer, Reserve(2), value);

    /// <summary>Writes an <c>i16</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt16(short value) => LittleEndian.WriteUInt16(_buffer, Reserve(2), (ushort)value);

    /// <summary>Writes a <c>u32</c> field, or a <c>str</c> field (a string table index).</summary>
    /// <param name="value">The value.</param>
    public void WriteUInt32(uint value) => LittleEndian.WriteUInt32(_buffer, Reserve(4), value);

    /// <summary>Writes an <c>i32</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt32(int value) => LittleEndian.WriteUInt32(_buffer, Reserve(4), (uint)value);

    /// <summary>Writes a <c>u64</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteUInt64(ulong value) => LittleEndian.WriteUInt64(_buffer, Reserve(8), value);

    /// <summary>Writes an <c>i64</c> field.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt64(long value) => LittleEndian.WriteUInt64(_buffer, Reserve(8), (ulong)value);

    /// <summary>Writes an <c>f32</c> field: the value's IEEE 754 bits, unchanged.</summary>
    /// <param name="value">The value.</param>
    public void WriteSingle(float value) => LittleEndian.WriteUInt32(_buffer, Reserve(4), FloatBits.ToBits(value));

    /// <summary>Writes an <c>f64</c> field: the value's IEEE 754 bits, unchanged.</summary>
    /// <param name="value">The value.</param>
    public void WriteDouble(double value) =>
        LittleEndian.WriteUInt64(_buffer, Reserve(8), (ulong)BitConverter.DoubleToInt64Bits(value));

    /// <summary>Writes bytes as they are, such as a fixed-size byte array field.</summary>
    /// <param name="source">The array that holds the bytes.</param>
    /// <param name="offset">The index of the first byte in <paramref name="source"/>.</param>
    /// <param name="count">The number of bytes.</param>
    public void WriteBytes(byte[] source, int offset, int count)
    {
        Guard.NotNull(source, nameof(source));
        if (offset < 0 || count < 0 || count > source.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The range lies outside the source array.");
        }

        Buffer.BlockCopy(source, offset, _buffer, Reserve(count), count);
    }

    /// <summary>Writes zero bytes: padding or reserved fields.</summary>
    /// <param name="count">The number of bytes.</param>
    public void WriteZeros(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count can't be negative.");
        }

        Array.Clear(_buffer, Reserve(count), count);
    }

    /// <summary>Writes the remaining records and the record count, which completes the file.</summary>
    /// <exception cref="InvalidOperationException">A record is in progress, or the file is already finished.</exception>
    public void Finish()
    {
        ThrowIfClosed();
        if (_recordStart != NoRecord)
        {
            throw new InvalidOperationException("The last record isn't ended; call EndRecord or DiscardRecord first.");
        }

        FlushBuffer();
        long end = _stream.Position;
        byte[] count = new byte[8];
        LittleEndian.WriteUInt64(count, 0, (ulong)_recordCount);
        _stream.Position = _headerPosition + RecordFileFormat.RecordCountOffset;
        _stream.Write(count, 0, count.Length);
        _stream.Position = end;
        _stream.Flush();
        _finished = true;
    }

    /// <summary>
    /// Closes the writer. If <see cref="Finish"/> wasn't called, the records ended so far are written, but
    /// the file stays unfinished and readers reject it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_finished)
            {
                if (_recordStart != NoRecord)
                {
                    _position = _recordStart;
                    _recordStart = NoRecord;
                }

                FlushBuffer();
                _stream.Flush();
            }
        }
        finally
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
    }

    private static void ValidateLayout(string recordType, int recordSize)
    {
        Guard.NotNull(recordType, nameof(recordType));
        if (!DumpNames.IsValidRecordType(recordType))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid record type name: {1}.", recordType, DumpNames.RecordTypeRule),
                nameof(recordType));
        }

        if (recordSize < 1 || recordSize > RecordFileFormat.MaxRecordSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordSize),
                recordSize,
                string.Format(CultureInfo.InvariantCulture, "A record has 1 to {0} bytes.", RecordFileFormat.MaxRecordSize));
        }
    }

    private void WriteHeader()
    {
        byte[] header = new byte[RecordFileFormat.HeaderSize];
        for (int i = 0; i < RecordFileFormat.Magic.Length; i++)
        {
            header[i] = (byte)RecordFileFormat.Magic[i];
        }

        LittleEndian.WriteUInt16(header, RecordFileFormat.VersionOffset, RecordFileFormat.ContainerVersion);
        LittleEndian.WriteUInt16(header, RecordFileFormat.HeaderSizeOffset, RecordFileFormat.HeaderSize);
        LittleEndian.WriteUInt32(header, RecordFileFormat.FlagsOffset, 0);
        LittleEndian.WriteUInt32(header, RecordFileFormat.RecordSizeOffset, (uint)RecordSize);
        LittleEndian.WriteUInt64(header, RecordFileFormat.RecordCountOffset, RecordFileFormat.UnfinishedRecordCount);

        // The name is validated ASCII; the rest of its field stays zero.
        for (int i = 0; i < RecordType.Length; i++)
        {
            header[RecordFileFormat.RecordTypeOffset + i] = (byte)RecordType[i];
        }

        _stream.Write(header, 0, header.Length);
    }

    private int Reserve(int size)
    {
        ThrowIfNoRecord();
        int offset = _position;
        if (offset + size > _recordStart + RecordSize)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "A {0}-byte field at offset {1} doesn't fit into a {2}-byte '{3}' record.",
                size,
                offset - _recordStart,
                RecordSize,
                RecordType));
        }

        _position = offset + size;
        return offset;
    }

    private void FlushBuffer()
    {
        if (_position > 0)
        {
            _stream.Write(_buffer, 0, _position);
            _position = 0;
        }
    }

    private void ThrowIfClosed()
    {
        Guard.NotDisposed(_disposed, this);
        if (_finished)
        {
            throw new InvalidOperationException("The record file is finished; nothing more can be written.");
        }
    }

    private void ThrowIfNoRecord()
    {
        ThrowIfClosed();
        if (_recordStart == NoRecord)
        {
            throw new InvalidOperationException("No record is in progress; call BeginRecord first.");
        }
    }
}
