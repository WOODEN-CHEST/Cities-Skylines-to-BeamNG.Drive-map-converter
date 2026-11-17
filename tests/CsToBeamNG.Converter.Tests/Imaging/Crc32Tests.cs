using System;
using System.Text;
using CsToBeamNG.Converter.Imaging;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Imaging;

public sealed class Crc32Tests
{
    [Fact]
    public void GivesTheStandardCheckValue() => Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    [Fact]
    public void GivesTheCrcOfThePngEndChunk() => Assert.Equal(0xAE426082u, Crc32.Compute("IEND"u8));

    [Fact]
    public void IsZeroForNoBytes() => Assert.Equal(0u, Crc32.Compute([]));

    [Fact]
    public void ContinuesWithMoreBytes()
    {
        byte[] text = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");

        Assert.Equal(0x414FA339u, Crc32.Compute(text));
        Assert.Equal(Crc32.Compute(text), Crc32.Append(Crc32.Compute(text.AsSpan(0, 10)), text.AsSpan(10)));
    }
}
