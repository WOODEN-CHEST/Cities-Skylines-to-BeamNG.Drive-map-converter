using System.Reflection;
using System.Runtime.Versioning;
using CsToBeamNG.Dump;
using Xunit;

namespace CsToBeamNG.Converter.Tests;

public sealed class DumpLibraryTests
{
    // Dump targets net35 (for the exporter) and net10.0. Converter code must bind to the net10.0 build.
    [Fact]
    public void ConverterSideLoadsTheNet10BuildOfDump()
    {
        TargetFrameworkAttribute? targetFramework =
            typeof(DumpFormat).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.Equal(".NETCoreApp,Version=v10.0", targetFramework?.FrameworkName);
    }
}
