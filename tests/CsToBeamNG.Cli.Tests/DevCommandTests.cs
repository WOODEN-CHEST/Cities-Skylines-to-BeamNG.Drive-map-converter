using System;
using System.IO;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class DevCommandTests
{
    [Fact]
    public void InstallsTheHelloLevelAndThenUpdatesIt()
    {
        using var temp = new TempFolder();
        string userDir = Directory.CreateDirectory(temp.PathOf("current")).FullName;
        string levelDir = Path.Join(userDir, "mods", "unpacked", "cstobeamng_hello", "levels", "cstobeamng_hello");

        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, DevCommand.InstallHelloLevel(OverrideUserDir(userDir), output, error));
        Assert.True(File.Exists(Path.Join(levelDir, "info.json")));
        Assert.True(File.Exists(Path.Join(levelDir, "terrain.ter")));
        Assert.StartsWith("Installed the level cstobeamng_hello: 14 files, 3.2 MB.", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());

        var again = new StringWriter();
        Assert.Equal(0, DevCommand.InstallHelloLevel(OverrideUserDir(userDir), again, error));
        Assert.Contains("Updated the previous install: 14 files replaced, 0 no longer needed removed.", again.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenTheUserFolderIsMissing()
    {
        using var temp = new TempFolder();
        var error = new StringWriter();

        Assert.Equal(DevCommand.FailedExitCode, DevCommand.InstallHelloLevel(OverrideUserDir(temp.PathOf("missing")), new StringWriter(), error));
        Assert.StartsWith("error: BeamNG.drive's user folder wasn't found.", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToReplaceAFolderItDidntCreate()
    {
        using var temp = new TempFolder();
        string userDir = Directory.CreateDirectory(temp.PathOf("current")).FullName;
        string modDir = Directory.CreateDirectory(Path.Join(userDir, "mods", "unpacked", "cstobeamng_hello")).FullName;
        File.WriteAllText(Path.Join(modDir, "readme.txt"), "someone else's");
        var error = new StringWriter();

        Assert.Equal(DevCommand.FailedExitCode, DevCommand.InstallHelloLevel(OverrideUserDir(userDir), new StringWriter(), error));
        Assert.Contains("wasn't created by this tool", error.ToString(), StringComparison.Ordinal);
        Assert.Equal("someone else's", File.ReadAllText(Path.Join(modDir, "readme.txt")));
    }

    private static DiscoveryOverrides OverrideUserDir(string path) =>
        new() { BeamNGUserDir = new PathOverride(path, "--beamng-user-dir") };
}
