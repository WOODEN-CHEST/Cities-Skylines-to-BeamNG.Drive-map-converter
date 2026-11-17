using System;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpVersionTests
{
    [Theory]
    [InlineData("0.1", 0, 1)]
    [InlineData("1.0", 1, 0)]
    [InlineData("12.345", 12, 345)]
    public void ParsesAndFormatsMajorDotMinor(string text, int major, int minor)
    {
        DumpVersion version = DumpVersion.Parse(text);

        Assert.Equal(new DumpVersion(major, minor), version);
        Assert.Equal(text, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.")]
    [InlineData(".1")]
    [InlineData("1.2.3")]
    [InlineData("01.1")]
    [InlineData("1.01")]
    [InlineData("-1.0")]
    [InlineData("+1.0")]
    [InlineData(" 1.0")]
    [InlineData("1.0 ")]
    [InlineData("1,0")]
    [InlineData("a.b")]
    [InlineData("1234567890.0")]
    public void RejectsTextThatIsNotMajorDotMinor(string text)
    {
        Assert.False(DumpVersion.TryParse(text, out _));
        Assert.Throws<FormatException>(() => DumpVersion.Parse(text));
    }

    [Fact]
    public void RejectsNullAndNegativeNumbers()
    {
        Assert.False(DumpVersion.TryParse(null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DumpVersion(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DumpVersion(0, -1));
    }

    [Fact]
    public void OrdersByMajorThenMinor()
    {
        var older = new DumpVersion(0, 9);
        var newer = new DumpVersion(1, 0);

        Assert.True(older < newer);
        Assert.True(newer > older);
        Assert.True(new DumpVersion(1, 2) > new DumpVersion(1, 1));
        Assert.True(older <= new DumpVersion(0, 9));
        Assert.True(older >= new DumpVersion(0, 9));
        Assert.True(older != newer);
    }

    [Fact]
    public void ReadsDataOfTheSameMajorVersionOnly()
    {
        var reader = new DumpVersion(1, 3);

        Assert.True(reader.CanRead(new DumpVersion(1, 0)));
        Assert.True(reader.CanRead(new DumpVersion(1, 7)));
        Assert.False(reader.CanRead(new DumpVersion(0, 3)));
        Assert.False(reader.CanRead(new DumpVersion(2, 0)));
    }
}
