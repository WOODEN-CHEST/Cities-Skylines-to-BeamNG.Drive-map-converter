using System;
using System.CommandLine;
using System.IO;
using CsToBeamNG.Converter.Conversion;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class ConvertCommandTests
{
    // An area on the test dump's 960 m detail lattice, small enough for a 512 × 512 terrain.
    private const string SmallArea = "0,0,500,500";

    [Fact]
    public void ConvertsADumpAndInstallsTheLevel()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"), terrainAndWater: true);
        string userDir = Directory.CreateDirectory(temp.PathOf("current")).FullName;
        string levelDir = Path.Join(userDir, "mods", "unpacked", "cstobeamng_synthetic_city", "levels", "cstobeamng_synthetic_city");
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(0, ConvertCommand.Convert(Request(folder, userDir), output, error));

        Assert.Empty(error.ToString());
        Assert.True(File.Exists(Path.Join(levelDir, "terrain.ter")));
        Assert.True(File.Exists(Path.Join(levelDir, "info.json")));
        Assert.StartsWith("CsToBeamNG conversion report\n", File.ReadAllText(Path.Join(levelDir, ConversionReport.FileName)), StringComparison.Ordinal);
        string text = output.ToString();
        Assert.StartsWith("Converting Synthetic City (dump) into the level cstobeamng_synthetic_city.", text, StringComparison.Ordinal);
        Assert.Contains("  Planning the terrain...", text, StringComparison.Ordinal);
        Assert.Contains("  Terrain: 512 × 512 samples, 1 m apart, 511 m across;", text, StringComparison.Ordinal);
        Assert.Contains("Installed the level cstobeamng_synthetic_city: 11 files,", text, StringComparison.Ordinal);
        Assert.Contains("  Report: " + Path.Join(levelDir, ConversionReport.FileName), text, StringComparison.Ordinal);

        var again = new StringWriter();
        Assert.Equal(0, ConvertCommand.Convert(Request(folder, userDir), again, error));
        Assert.Contains("Updated the previous install: 11 files replaced, 0 no longer needed removed.", again.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NamesTheLevelAsAsked()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"), terrainAndWater: true);
        string userDir = Directory.CreateDirectory(temp.PathOf("current")).FullName;

        Assert.Equal(0, ConvertCommand.Convert(Request(folder, userDir) with { Name = "my_city" }, new StringWriter(), new StringWriter()));

        Assert.True(File.Exists(Path.Join(userDir, "mods", "unpacked", "cstobeamng_my_city", "levels", "cstobeamng_my_city", "terrain.ter")));
    }

    [Fact]
    public void ExplainsADumpWithoutTerrain()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"));
        string userDir = Directory.CreateDirectory(temp.PathOf("current")).FullName;
        var error = new StringWriter();

        Assert.Equal(ConvertCommand.FailedExitCode, ConvertCommand.Convert(Request(folder, userDir), new StringWriter(), error));
        Assert.StartsWith("error: The dump has no terrain", error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Join(userDir, "mods", "unpacked", "cstobeamng_synthetic_city")));
    }

    [Fact]
    public void ExplainsAMissingUserFolder()
    {
        using var temp = new TempFolder();
        string folder = TestDumps.Write(temp.PathOf("dump"), terrainAndWater: true);
        var error = new StringWriter();

        Assert.Equal(ConvertCommand.FailedExitCode, ConvertCommand.Convert(Request(folder, temp.PathOf("missing")), new StringWriter(), error));
        Assert.StartsWith("error: BeamNG.drive's user folder wasn't found.", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--name", "San-Palma", "'San-Palma' can't name a level")]
    [InlineData("--max-terrain-size", "3000", "--max-terrain-size must be a power of two from 256 to 8192.")]
    [InlineData("--max-terrain-size", "128", "--max-terrain-size must be a power of two from 256 to 8192.")]
    [InlineData("--area", "nowhere", "'nowhere' is not an area.")]
    public void RejectsInvalidOptions(string option, string value, string message)
    {
        ParseResult result = ConvertCommand.Create().Parse(["dump", option, value]);

        Assert.Contains(result.Errors, e => e.Message.StartsWith(message, StringComparison.Ordinal));
    }

    [Fact]
    public void ParsesItsOptions()
    {
        ParseResult result = ConvertCommand.Create().Parse(["dump", "--name", "palma", "--area", "3-5,4", "--max-terrain-size", "4096", "--beamng-user-dir", "/games/current"]);

        Assert.Empty(result.Errors);
        Assert.Equal("palma", result.GetValue<string?>("--name"));
        Assert.Equal("3-5,4", result.GetValue<RenderArea>("--area")!.Text);
        Assert.Equal(4096, result.GetValue<int>("--max-terrain-size"));
        Assert.Same(RenderArea.AreaOfInterest, ConvertCommand.Create().Parse(["dump"]).GetValue<RenderArea>("--area"));
    }

    private static ConvertRequest Request(string folder, string userDir)
    {
        Assert.True(MapAreaSyntax.TryParseArea(SmallArea, out RenderArea? area, out _));
        return new ConvertRequest(folder)
        {
            Area = area,
            Locations = new DiscoveryOverrides { BeamNGUserDir = new PathOverride(userDir, "--beamng-user-dir") },
        };
    }
}
