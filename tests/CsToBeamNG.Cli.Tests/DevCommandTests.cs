using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CsToBeamNG.Converter.Development.HeightProbe;
using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Dump;
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
    public void ExplainsAMissingGeometryProbe()
    {
        using var temp = new TempFolder();
        var error = new StringWriter();

        Assert.Equal(DevCommand.FailedExitCode, DevCommand.CheckGeometryProbe(temp.Root, null, new StringWriter(), error));
        Assert.StartsWith("error: '", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("geometry-probe.json' couldn't be read", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ChecksAHeightProbe()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"), terrainAndWater: true);
        CityTerrain terrain = WorldLoader.Load(folder).World.Find<CityTerrain>()!;
        WriteHeightProbe(folder, terrain, groundError: 0.001);
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(0, DevCommand.CheckHeightProbe(folder, output, error));
        Assert.Empty(error.ToString());
        Assert.StartsWith("Heights probe from game 1.21.1-f9: 2 positions.\n", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("  rawVsRaw2                   0 of     1000 values, 0 to 0 m\n", output.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("All 3 samplers match within 5 mm.\n", output.ToString(), StringComparison.Ordinal);

        WriteHeightProbe(folder, terrain, groundError: 0.01);
        Assert.Equal(DevCommand.FailedExitCode, DevCommand.CheckHeightProbe(folder, output, error));
        Assert.EndsWith("1 of 3 samplers don't match.\n", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainsAMissingHeightProbeOrTerrain()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"));
        var error = new StringWriter();

        Assert.Equal(DevCommand.FailedExitCode, DevCommand.CheckHeightProbe(folder, new StringWriter(), error));
        Assert.Contains("couldn't be read", error.ToString(), StringComparison.Ordinal);

        WriteHeightProbe(folder, null, groundError: 0);
        error = new StringWriter();
        Assert.Equal(DevCommand.FailedExitCode, DevCommand.CheckHeightProbe(folder, new StringWriter(), error));
        Assert.Contains("has no terrain the converter could load", error.ToString(), StringComparison.Ordinal);
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

    // A heights probe with two positions whose game heights are the converter's, the ground off by an error.
    private static void WriteHeightProbe(string dumpFolder, CityTerrain? terrain, double groundError)
    {
        var samples = new List<Dictionary<string, object>>();
        foreach ((double x, double z) in new[] { (-100.0, 50.0), (3000.0, -2000.0) })
        {
            samples.Add(new Dictionary<string, object>
            {
                ["kind"] = "node",
                ["id"] = samples.Count,
                ["label"] = "Basic Road",
                ["flags"] = "00004001",
                ["x"] = x,
                ["y"] = 0,
                ["z"] = z,
                ["detail"] = (terrain?.SampleGround(x, z) ?? 0) + groundError,
                ["final"] = terrain?.FinalHeights.Sample(x, z) ?? 0,
                ["raw"] = terrain?.RawHeights.Sample(x, z) ?? 0,
                ["rawSmooth"] = 0,
                ["blockSmooth"] = 0,
            });
        }

        string folder = Directory.CreateDirectory(Path.Join(dumpFolder, DumpProbes.FolderName, DumpProbes.Heights)).FullName;
        File.WriteAllText(Path.Join(folder, DumpProbes.HeightsFileName), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["format"] = HeightProbeFile.FormatName,
            ["version"] = HeightProbeFile.FormatVersion,
            ["game"] = "1.21.1-f9",
            ["arrays"] = new Dictionary<string, object>
            {
                ["rawVsRaw2"] = new Dictionary<string, object> { ["values"] = 1000, ["differing"] = 0, ["maxAbove"] = 0, ["maxBelow"] = 0 },
            },
            ["samples"] = samples,
        }));
    }

    private static DiscoveryOverrides OverrideUserDir(string path) =>
        new() { BeamNGUserDir = new PathOverride(path, "--beamng-user-dir") };
}
