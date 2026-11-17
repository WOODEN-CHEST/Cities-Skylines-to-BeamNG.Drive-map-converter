using System.Diagnostics;
using System.IO;
using CsToBeamNG.Converter.Automation;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Automation;

public sealed class CitiesSkylinesLauncherTests
{
    [Theory]
    [InlineData(OsKind.Linux, "Cities.x64", "Cities.x64")]
    [InlineData(OsKind.Windows, "Cities.exe", "Cities")]
    public void StartsTheNativeExecutable(OsKind os, string executable, string processName)
    {
        string installDir = Path.Join("games", "Cities_Skylines");
        var launcher = new CitiesSkylinesLauncher(installDir, os);

        ProcessStartInfo startInfo = launcher.CreateStartInfo();

        Assert.Equal(Path.Join(installDir, executable), startInfo.FileName);
        Assert.Equal(installDir, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Arguments);
        Assert.Equal(processName, CitiesSkylinesLauncher.GetProcessName(os));
    }

    [Fact]
    public void TellsSteamApiWhichGameItIs()
    {
        ProcessStartInfo startInfo = new CitiesSkylinesLauncher("games", OsKind.Linux).CreateStartInfo();

        Assert.Equal("255710", startInfo.Environment["SteamAppId"]);
        Assert.Equal("255710", startInfo.Environment["SteamGameId"]);
    }

    [Fact]
    public void RefusesAMissingExecutable()
    {
        using var temp = new TempFolder();
        var launcher = new CitiesSkylinesLauncher(temp.Root, OsKind.Linux);

        var exception = Assert.Throws<FileNotFoundException>(launcher.Start);

        Assert.Contains("Cities.x64", exception.Message, System.StringComparison.Ordinal);
    }
}
