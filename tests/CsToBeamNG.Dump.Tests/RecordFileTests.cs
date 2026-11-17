using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using CsToBeamNG.Dump.Records;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class RecordFileTests
{
    private const int AllTypesSize = 1 + 1 + 2 + 2 + 4 + 4 + 8 + 8 + 4 + 8 + 3 + 1;

    [Fact]
    public void WritesTheDocumentedHeader()
    {
        byte[] file = WriteToBytes("netNode", 12, _ => { });

        Assert.Equal(64, file.Length);
        Assert.Equal("CSBD"u8.ToArray(), file[..4]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(4)));
        Assert.Equal(64, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(6)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(8)));
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12)));
        Assert.Equal(0ul, BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(16)));
        Assert.Equal("netNode"u8.ToArray(), file[24..31]);
        Assert.True(file.AsSpan(31).IndexOfAnyExcept((byte)0) < 0);
    }

    [Fact]
    public void WritesFieldsLittleEndianWithRecordsBackToBack()
    {
        byte[] file = WriteToBytes("pair", 8, writer =>
        {
            writer.BeginRecord();
            writer.WriteUInt32(0x11223344);
            writer.WriteSingle(1f);
            writer.EndRecord();
            writer.BeginRecord();
            writer.WriteUInt32(5);
            writer.WriteSingle(-2f);
            writer.EndRecord();
        });

        Assert.Equal(2ul, BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(16)));
        Assert.Equal([0x44, 0x33, 0x22, 0x11, 0x00, 0x00, 0x80, 0x3F, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0], file[64..]);
    }

    [Fact]
    public void RoundTripsEveryFieldType()
    {
        float nanWithPayload = BitConverter.Int32BitsToSingle(0x7FC0_0001);
        byte[] raw = [1, 2, 3];
        byte[] file = WriteToBytes("allTypes", AllTypesSize, writer =>
        {
            writer.BeginRecord();
            writer.WriteByte(byte.MaxValue);
            writer.WriteSByte(sbyte.MinValue);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteInt16(short.MinValue);
            writer.WriteUInt32(uint.MaxValue);
            writer.WriteInt32(int.MinValue);
            writer.WriteUInt64(ulong.MaxValue);
            writer.WriteInt64(long.MinValue);
            writer.WriteSingle(nanWithPayload);
            writer.WriteDouble(-double.Epsilon);
            writer.WriteBytes(raw, 0, raw.Length);
            writer.WriteZeros(1);
            writer.EndRecord();
        });

        using RecordFileReader reader = Read(file);
        Assert.Equal("allTypes", reader.RecordType);
        Assert.Equal(AllTypesSize, reader.RecordSize);
        Assert.Equal(1, reader.RecordCount);
        Assert.True(reader.MoveNext());
        Assert.Equal(byte.MaxValue, reader.ReadByte());
        Assert.Equal(sbyte.MinValue, reader.ReadSByte());
        Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
        Assert.Equal(short.MinValue, reader.ReadInt16());
        Assert.Equal(uint.MaxValue, reader.ReadUInt32());
        Assert.Equal(int.MinValue, reader.ReadInt32());
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
        Assert.Equal(long.MinValue, reader.ReadInt64());
        Assert.Equal(BitConverter.SingleToInt32Bits(nanWithPayload), BitConverter.SingleToInt32Bits(reader.ReadSingle()));
        Assert.Equal(-double.Epsilon, reader.ReadDouble());
        byte[] copy = new byte[3];
        reader.ReadBytes(copy, 0, copy.Length);
        Assert.Equal(raw, copy);
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(0, reader.RemainingInRecord);
        Assert.False(reader.MoveNext());
    }

    [Fact]
    public void RoundTripsAFileWithoutRecords()
    {
        byte[] file = WriteToBytes("empty", 4, _ => { });

        using (RecordFileReader reader = Read(file))
        {
            Assert.Equal(0, reader.RecordCount);
            Assert.False(reader.MoveNext());
            Assert.Equal(0, reader.RecordIndex);
        }

        using (RecordFileReader reader = Read(file))
        {
            Assert.Empty(reader.ReadAllRecords<int>());
        }
    }

    [Fact]
    public void RoundTripsManyRecordsThroughAFile()
    {
        // Many buffer fills, and 20-byte records don't divide the buffer evenly.
        const int Count = 250_000;
        using var folder = new TempFolder();
        string path = folder.PathOf("samples.bin");
        using (RecordFileWriter writer = RecordFileWriter.Create(path, "sample", 20))
        {
            for (int i = 0; i < Count; i++)
            {
                writer.BeginRecord();
                writer.WriteUInt32((uint)i);
                writer.WriteSingle(i * 0.5f);
                writer.WriteDouble(i / 3.0);
                writer.WriteInt32(-i);
                writer.EndRecord();
            }

            writer.Finish();
        }

        Assert.Equal(64 + (Count * 20L), new FileInfo(path).Length);
        using (RecordFileReader reader = RecordFileReader.Open(path, "sample", 20))
        {
            int index = 0;
            int mismatches = 0;
            while (reader.MoveNext())
            {
                if (reader.ReadUInt32() != (uint)index || reader.ReadSingle() != index * 0.5f
                    || reader.ReadDouble() != index / 3.0 || reader.ReadInt32() != -index)
                {
                    mismatches++;
                }

                index++;
            }

            Assert.Equal(Count, index);
            Assert.Equal(0, mismatches);
        }

        using (RecordFileReader reader = RecordFileReader.Open(path))
        {
            SampleRecord[] records = reader.ReadAllRecords<SampleRecord>();
            Assert.Equal(Count, records.Length);
            Assert.Equal(new SampleRecord { Id = 123_456, Half = 61_728f, Third = 123_456 / 3.0, Negative = -123_456 }, records[123_456]);
            Assert.Equal(new SampleRecord { Id = Count - 1, Half = (Count - 1) * 0.5f, Third = (Count - 1) / 3.0, Negative = -(Count - 1) }, records[^1]);
        }
    }

    [Fact]
    public void HandlesRecordsLargerThanTheBuffer()
    {
        const int Size = 100_000;
        byte[] payload = new byte[Size];
        byte[] file = WriteToBytes("big", Size, writer =>
        {
            for (int i = 1; i <= 3; i++)
            {
                Array.Fill(payload, (byte)i);
                writer.BeginRecord();
                writer.WriteBytes(payload, 0, Size);
                writer.EndRecord();
            }
        });

        using RecordFileReader reader = Read(file);
        byte[] copy = new byte[Size];
        for (int i = 1; i <= 3; i++)
        {
            Assert.True(reader.MoveNext());
            reader.ReadBytes(copy, 0, Size);
            Assert.True(copy.AsSpan().IndexOfAnyExcept((byte)i) < 0);
        }

        Assert.False(reader.MoveNext());
    }

    [Fact]
    public void SkipsFieldsThatANewerMinorVersionAppended()
    {
        // Version x.1 of a layout appended a u32 to the 8-byte records of version x.0.
        byte[] file = WriteToBytes("point", 12, writer =>
        {
            for (uint i = 1; i <= 3; i++)
            {
                writer.BeginRecord();
                writer.WriteUInt32(i);
                writer.WriteSingle(i * 10f);
                writer.WriteUInt32(0xDEADBEEF);
                writer.EndRecord();
            }
        });

        using (RecordFileReader reader = Read(file))
        {
            reader.CheckLayout("point", 8);
            for (uint i = 1; i <= 3; i++)
            {
                Assert.True(reader.MoveNext());
                Assert.Equal(i, reader.ReadUInt32());
                Assert.Equal(i * 10f, reader.ReadSingle());
                Assert.Equal(4, reader.RemainingInRecord);
            }

            Assert.False(reader.MoveNext());
        }

        using (RecordFileReader reader = Read(file))
        {
            PointVersion0[] points = reader.ReadAllRecords<PointVersion0>();
            Assert.Equal([new PointVersion0 { Id = 1, X = 10f }, new PointVersion0 { Id = 2, X = 20f }, new PointVersion0 { Id = 3, X = 30f }], points);
        }
    }

    [Fact]
    public void ReadAllRecordsNeedsAStructNoLargerThanTheRecords()
    {
        using RecordFileReader reader = Read(WriteToBytes("small", 4, writer =>
        {
            writer.BeginRecord();
            writer.WriteUInt32(1);
            writer.EndRecord();
        }));

        Assert.Throws<InvalidOperationException>(() => reader.ReadAllRecords<SampleRecord>());
    }

    [Fact]
    public void RejectsReadingPastTheEndOfARecord()
    {
        using RecordFileReader reader = Read(WriteToBytes("small", 4, writer =>
        {
            writer.BeginRecord();
            writer.WriteUInt32(1);
            writer.EndRecord();
        }));

        Assert.Throws<InvalidOperationException>(() => reader.ReadUInt32());
        Assert.True(reader.MoveNext());
        reader.ReadUInt16();
        Assert.Throws<InvalidOperationException>(() => reader.ReadUInt32());
        Assert.Equal(2, reader.RemainingInRecord);
    }

    [Fact]
    public void RejectsAFileThatIsNotARecordFile()
    {
        byte[] zip = new byte[100];
        zip[0] = 0x50;
        zip[1] = 0x4B;
        zip[2] = 0x03;
        zip[3] = 0x04;

        DumpException error = Assert.Throws<DumpException>(() => Read(zip));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("'test.bin'", error.Message, StringComparison.Ordinal);
        Assert.Contains("50 4B 03 04", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(30)]
    [InlineData(63)]
    public void RejectsATruncatedHeader(int length)
    {
        byte[] file = WriteToBytes("netNode", 4, _ => { })[..length];

        Assert.Equal(DumpErrorKind.Corrupt, Assert.Throws<DumpException>(() => Read(file)).Kind);
    }

    [Fact]
    public void RejectsTruncatedRecords()
    {
        byte[] file = WriteToBytes("value", 4, writer =>
        {
            for (int i = 0; i < 10; i++)
            {
                writer.BeginRecord();
                writer.WriteInt32(i);
                writer.EndRecord();
            }
        });
        byte[] truncated = file[..^5];

        DumpException checkedUpFront = Assert.Throws<DumpException>(() => Read(truncated));
        Assert.Equal(DumpErrorKind.Corrupt, checkedUpFront.Kind);
        Assert.Contains("truncated", checkedUpFront.Message, StringComparison.Ordinal);

        // A stream without a length: the reader notices when the data ends.
        using var reader = new RecordFileReader(new ForwardOnlyStream(truncated), "test.bin", leaveOpen: false);
        DumpException whileReading = Assert.Throws<DumpException>(() =>
        {
            while (reader.MoveNext())
            {
            }
        });
        Assert.Equal(DumpErrorKind.Corrupt, whileReading.Kind);
        Assert.Contains("ends after 8 of 10 records", whileReading.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsBytesAfterTheLastRecord()
    {
        byte[] valid = WriteToBytes("value", 4, writer =>
        {
            writer.BeginRecord();
            writer.WriteInt32(1);
            writer.EndRecord();
        });

        DumpException error = Assert.Throws<DumpException>(() => Read([.. valid, 0, 0, 0]));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("3 unexpected bytes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFileThatWasNeverFinished()
    {
        using var stream = new MemoryStream();
        using (var writer = new RecordFileWriter(stream, "value", 4, leaveOpen: true))
        {
            writer.BeginRecord();
            writer.WriteInt32(1);
            writer.EndRecord();

            // Interrupted in the middle of the second record.
            writer.BeginRecord();
            writer.WriteInt16(2);
        }

        byte[] file = stream.ToArray();

        // The complete record is kept, for diagnosis, but the count says "unfinished".
        Assert.Equal(64 + 4, file.Length);
        Assert.Equal(RecordFileFormat.UnfinishedRecordCount, BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(16)));
        Assert.Equal(DumpErrorKind.Incomplete, Assert.Throws<DumpException>(() => Read(file)).Kind);
    }

    [Theory]
    [InlineData(4, 2, DumpErrorKind.Incompatible)]
    [InlineData(6, 32, DumpErrorKind.Corrupt)]
    [InlineData(8, 1, DumpErrorKind.Incompatible)]
    [InlineData(12, 0, DumpErrorKind.Corrupt)]
    [InlineData(23, 0x40, DumpErrorKind.Corrupt)]
    [InlineData(24, '1', DumpErrorKind.Corrupt)]
    [InlineData(63, 1, DumpErrorKind.Corrupt)]
    public void RejectsUnsupportedOrDamagedHeaders(int offset, int value, DumpErrorKind expected)
    {
        // Container version 2; header size 32; a flag; record size 0; a record count of 2^62;
        // a record type starting with a digit; padding that isn't zero.
        byte[] file = WriteToBytes("value", 4, _ => { });
        file[offset] = (byte)value;

        Assert.Equal(expected, Assert.Throws<DumpException>(() => Read(file)).Kind);
    }

    [Fact]
    public void ChecksTheExpectedRecordTypeAndMinimumSize()
    {
        using RecordFileReader reader = Read(WriteToBytes("netNode", 8, _ => { }));

        reader.CheckLayout("netNode", 8);
        reader.CheckLayout("netNode", 4);
        Assert.Equal(DumpErrorKind.Corrupt, Assert.Throws<DumpException>(() => reader.CheckLayout("netSegment", 8)).Kind);
        Assert.Equal(DumpErrorKind.Corrupt, Assert.Throws<DumpException>(() => reader.CheckLayout("netNode", 12)).Kind);
    }

    [Fact]
    public void WriterInsistsOnWholeRecords()
    {
        using var stream = new MemoryStream();
        using var writer = new RecordFileWriter(stream, "pair", 8, leaveOpen: true);

        Assert.Throws<InvalidOperationException>(() => writer.WriteUInt32(1));
        Assert.Throws<InvalidOperationException>(writer.EndRecord);
        writer.BeginRecord();
        Assert.Throws<InvalidOperationException>(writer.BeginRecord);
        writer.WriteUInt32(1);
        Assert.Throws<InvalidOperationException>(() => writer.WriteUInt64(2));
        Assert.Throws<InvalidOperationException>(writer.EndRecord);
        Assert.Throws<InvalidOperationException>(writer.Finish);
        writer.WriteUInt32(2);
        writer.EndRecord();
        writer.Finish();

        Assert.True(writer.IsFinished);
        Assert.Equal(1, writer.RecordCount);
        Assert.Throws<InvalidOperationException>(writer.BeginRecord);
    }

    [Fact]
    public void LeavesDiscardedRecordsOut()
    {
        byte[] file = WriteToBytes("value", 4, writer =>
        {
            foreach (int value in (int[])[1, 2, 3])
            {
                writer.BeginRecord();
                writer.WriteInt32(value);
                if (value == 2)
                {
                    writer.DiscardRecord();
                }
                else
                {
                    writer.EndRecord();
                }
            }
        });

        using RecordFileReader reader = Read(file);
        Assert.Equal([1, 3], reader.ReadAllRecords<int>());
    }

    [Theory]
    [InlineData("", 4)]
    [InlineData("1node", 4)]
    [InlineData("net node", 4)]
    [InlineData("netNöde", 4)]
    [InlineData("a1234567890123456789012345678901234567890", 4)]
    [InlineData("netNode", 0)]
    [InlineData("netNode", -1)]
    [InlineData("netNode", RecordFileFormat.MaxRecordSize + 1)]
    public void WriterRejectsInvalidLayouts(string recordType, int recordSize)
    {
        using var stream = new MemoryStream();

        Assert.ThrowsAny<ArgumentException>(() => new RecordFileWriter(stream, recordType, recordSize, leaveOpen: true));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void CreateRefusesToOverwriteAFile()
    {
        using var folder = new TempFolder();
        string path = folder.PathOf("existing.bin");
        File.WriteAllBytes(path, [1]);

        Assert.Throws<IOException>(() => RecordFileWriter.Create(path, "value", 4));
        Assert.Equal([1], File.ReadAllBytes(path));
    }

    [Fact]
    public void OpenReportsAMissingFile()
    {
        using var folder = new TempFolder();

        DumpException error = Assert.Throws<DumpException>(() => RecordFileReader.Open(folder.PathOf("missing.bin")));

        Assert.Equal(DumpErrorKind.NotFound, error.Kind);
    }

    private static byte[] WriteToBytes(string recordType, int recordSize, Action<RecordFileWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new RecordFileWriter(stream, recordType, recordSize, leaveOpen: true))
        {
            write(writer);
            writer.Finish();
        }

        return stream.ToArray();
    }

    private static RecordFileReader Read(byte[] file) => new(new MemoryStream(file), "test.bin", leaveOpen: false);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SampleRecord
    {
        public uint Id;
        public float Half;
        public double Third;
        public int Negative;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PointVersion0
    {
        public uint Id;
        public float X;
    }

    /// <summary>A stream that can't seek or tell its length, like a pipe.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
