using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpFormatTests
{
    [Fact]
    public void MajorVersionIsZeroDuringInitialDevelopment()
    {
        Assert.Equal(0, DumpFormat.MajorVersion);
    }

    // A change of the format version must come with a changelog entry in docs/dump-format.md.
    [Fact]
    public void CurrentVersionIsTheOneInTheSpecification()
    {
        Assert.Equal("0.1", DumpFormat.CurrentVersion.ToString());
    }
}
