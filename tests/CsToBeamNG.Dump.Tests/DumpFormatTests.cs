using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpFormatTests
{
    [Fact]
    public void MajorVersionIsZeroDuringInitialDevelopment()
    {
        Assert.Equal(0, DumpFormat.MajorVersion);
    }
}
