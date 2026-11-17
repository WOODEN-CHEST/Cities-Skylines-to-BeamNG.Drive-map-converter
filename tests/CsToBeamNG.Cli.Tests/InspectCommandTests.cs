using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class InspectCommandTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("en-US")]
    [InlineData("lv-LV")]
    [InlineData("sv-SE")]
    public void SummarisesADumpWithOnlyAManifest(string culture)
    {
        using var scope = new CultureScope(culture);
        string folder = TestDumps.Write(_temp.PathOf("dump"));

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder), _output, _error);

        Assert.Equal(0, exitCode);
        Assert.Empty(_error.ToString());
        Assert.Equal(
            $"""
            City dump {folder}

            Manifest
              Format:         0.1, complete
              Exported:       2026-11-17 12:00:00 UTC by CsToBeamNG.Exporter 0.1.0-test
              Game:           1.21.1-f9
              Mods:           none
              City:           Synthetic City
              Map:            Test Map
              Environment:    Sunny
              Map theme:      none
              Traffic:        right-hand
              Unlocked tiles: (4, 4)

            Objects in the city
              buildings      12
              trees      183346

            Sections
              none
              Not in the dump: terrain, water

            Area of interest
              Bounds:         x -960 to 960, z -960 to 960 (1920 × 1920 m)
              From:           the unlocked tiles; no section gives content bounds

            Warnings
              none

            """,
            Output());
    }

    [Fact]
    public void ReportsUnknownSectionsModsAndTheExportersProblems()
    {
        DumpManifest manifest = TestDumps.Manifest();
        manifest.Game.Mods.Add(new GameModInfo("81 Tiles 2", "2862121823"));
        manifest.Game.Mods.Add(new GameModInfo("Local Mod", null));
        manifest.City.MapTheme = "Alpine";
        manifest.City.LeftHandTraffic = true;
        manifest.Counts["props"] = 10;
        manifest.FailureCounts["props"] = 2;
        manifest.FailureCounts["vehicles"] = 1;
        manifest.Warnings.Add("2 props were skipped: their prefab is missing.");
        string folder = TestDumps.Write(_temp.PathOf("dump"), manifest, unknownSection: "mystery");
        long bytes = new FileInfo(Path.Join(folder, "mystery", "data.json")).Length;

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder), _output, _error);

        Assert.Equal(0, exitCode);
        string output = Output();
        Assert.Contains("  Mods:           2\n    81 Tiles 2 (workshop item 2862121823)\n    Local Mod\n", output, StringComparison.Ordinal);
        Assert.Contains("  Map theme:      Alpine\n  Traffic:        left-hand\n", output, StringComparison.Ordinal);
        Assert.Contains("  props          10  (2 skipped by the exporter)\n", output, StringComparison.Ordinal);
        Assert.Contains("  vehicles        ?  (1 skipped by the exporter)\n", output, StringComparison.Ordinal);
        Assert.Contains($"Sections\n  mystery 0.3: skipped, unknown to this version; 1 record in 1 file, {bytes} B\n", output, StringComparison.Ordinal);
        Assert.Contains(
            "Warnings\n"
            + "  warning: from the exporter: 2 props were skipped: their prefab is missing.\n"
            + "  note:    Section 'mystery' (version 0.3) is unknown to this version of CsToBeamNG, so it is skipped.\n",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAndDrawsTheTerrainAndTheWater()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = TestDumps.Write(_temp.PathOf("dump"), terrainAndWater: true);
        string image = _temp.PathOf("map.png");

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder) { RenderPath = image, RenderSize = 64, RenderArea = RenderArea.Map }, _output, _error);

        Assert.Equal(0, exitCode);
        Assert.Empty(_error.ToString());
        string output = Output();
        Assert.Contains("\n  terrain 0.1: loaded; 4 files, ", output, StringComparison.Ordinal);
        Assert.Contains(
            "    grid: 10 × 10 points, 1920 m apart, from (-8640, -8640)\n"
            + "    raw heights: 100 to 370 m\n"
            + "    final heights: 100 to 370 m\n"
            + "    levelled by roads and buildings: 0 points, raised up to 0 m, lowered up to 0 m\n"
            + "    detail heights: 1 tile: (4, 4); 960 m apart, 222 to 252 m\n"
            + "  water 0.1: loaded; 1 record in 3 files, ",
            output,
            StringComparison.Ordinal);
        Assert.Contains(
            "    sea level: 40 m\n"
            + "    grid: 10 × 10 points, 1920 m apart, from (-8640, -8640)\n"
            + "    cells with water: 50 of 100 (184.32 km²), up to 3 m deep\n"
            + "    water surface: 103 to 223 m\n"
            + "    water sources: 1: 1 natural, 0 of buildings, 0 of water treatment\n",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Not in the dump", output, StringComparison.Ordinal);
        Assert.Contains("\n    terrain: the ground the game draws", output, StringComparison.Ordinal);
        Assert.Contains("\n    water: water at least 0.31 m deep", output, StringComparison.Ordinal);
        Assert.Equal((64, 64), PngSize(image));
    }

    [Fact]
    public void WarnsAboutAnIncompleteDump()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"), complete: false);

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder), _output, _error);

        Assert.Equal(0, exitCode);
        Assert.Contains("  Format:         0.1, INCOMPLETE: the export didn't finish\n", Output(), StringComparison.Ordinal);
        Assert.Contains("  warning: The export that wrote", Output(), StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsTheManifestFile()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"));

        int exitCode = InspectCommand.Inspect(new InspectRequest(Path.Join(folder, "manifest.json")), _output, _error);

        Assert.Equal(0, exitCode);
        Assert.StartsWith($"City dump {folder}\n", Output(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainsAMissingDump()
    {
        int exitCode = InspectCommand.Inspect(new InspectRequest(_temp.PathOf("missing")), _output, _error);

        Assert.Equal(InspectCommand.FailedExitCode, exitCode);
        Assert.StartsWith("error: There is no dump at", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_output.ToString());
    }

    [Fact]
    public void ExplainsADamagedDump()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"), unknownSection: "mystery");
        File.AppendAllText(Path.Join(folder, "mystery", "data.json"), " ");

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder), _output, _error);

        Assert.Equal(InspectCommand.FailedExitCode, exitCode);
        Assert.Contains("but the manifest lists", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OverridesTheAreaOfInterestWithTiles()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"));
        Assert.True(MapAreaSyntax.TryParseTiles("3-5,4", out IReadOnlyList<TileCoordinate>? tiles, out _));

        InspectCommand.Inspect(new InspectRequest(folder) { Tiles = tiles }, _output, _error);

        Assert.Contains(
            "  Bounds:         x -2880 to 2880, z -960 to 960 (5760 × 1920 m)\n  From:           the tiles chosen with --tiles\n",
            Output(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RendersTheAreaOfInterest()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"));
        string image = _temp.PathOf("renders", "aoi.png");

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder) { RenderPath = image, RenderSize = 64 }, _output, _error);

        Assert.Equal(0, exitCode);
        Assert.Equal((64, 64), PngSize(image));
        Assert.Contains(
            $"Render\n  File:           {image}\n"
            + "  Area:           the area of interest: x -960 to 960, z -960 to 960\n"
            + "  Image:          64 × 64 pixels, 30 m per pixel; +X (east) right, +Z (north) up\n"
            + "  Layers:         bottom to top\n    ground: ",
            Output(),
            StringComparison.Ordinal);
        Assert.Contains("\n    axes: ", Output(), StringComparison.Ordinal);
    }

    [Fact]
    public void RendersTheWholeMap()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = TestDumps.Write(_temp.PathOf("dump"));
        string image = _temp.PathOf("map.png");

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder) { RenderPath = image, RenderSize = 100, RenderArea = RenderArea.Map }, _output, _error);

        Assert.Equal(0, exitCode);
        Assert.Equal((100, 100), PngSize(image));
        Assert.Contains("  Area:           the whole map: x -8640 to 8640, z -8640 to 8640\n", Output(), StringComparison.Ordinal);
        Assert.Contains("  Image:          100 × 100 pixels, 172.8 m per pixel;", Output(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAnImageItCantWrite()
    {
        string folder = TestDumps.Write(_temp.PathOf("dump"));
        File.WriteAllText(_temp.PathOf("blocker"), "a file, not a folder");

        int exitCode = InspectCommand.Inspect(new InspectRequest(folder) { RenderPath = _temp.PathOf("blocker", "out.png") }, _output, _error);

        Assert.Equal(InspectCommand.FailedExitCode, exitCode);
        Assert.StartsWith("error: the image couldn't be written to", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesTheRenderOptions()
    {
        ParseResult result = InspectCommand.Create().Parse(["dump", "--render", "out.PNG", "--size", "256", "--area", "-500,-300,1500,700", "--tiles", "3-5,4"]);

        Assert.Empty(result.Errors);
        Assert.Equal(256, result.GetValue<int>("--size"));
        Assert.Equal(new MapRect(-500, -300, 1500, 700), result.GetValue<RenderArea>("--area")?.Bounds);
        Assert.Equal(3, result.GetValue<IReadOnlyList<TileCoordinate>?>("--tiles")?.Count);
        Assert.Same(RenderArea.AreaOfInterest, InspectCommand.Create().Parse(["dump", "--render", "out.png"]).GetValue<RenderArea>("--area"));
        Assert.Equal(InspectCommand.DefaultRenderSize, InspectCommand.Create().Parse(["dump"]).GetValue<int>("--size"));
    }

    [Theory]
    [InlineData("--size and --area only apply to an image; add --render <file.png>.", "--size", "512")]
    [InlineData("--size and --area only apply to an image; add --render <file.png>.", "--area", "map")]
    [InlineData("--size must be from 16 to 8192 pixels.", "--render", "out.png", "--size", "8")]
    [InlineData("--size must be from 16 to 8192 pixels.", "--render", "out.png", "--size", "8193")]
    [InlineData("--render takes the name of a .png file.", "--render", "out.jpg")]
    [InlineData("'9,4' is not a block of tiles.", "--tiles", "9,4")]
    [InlineData("'nope' is not an area.", "--render", "out.png", "--area", "nope")]
    public void RejectsInvalidOptions(string message, params string[] options)
    {
        ParseResult result = InspectCommand.Create().Parse(["dump", .. options]);

        Assert.Contains(result.Errors, e => e.Message.StartsWith(message, StringComparison.Ordinal));
    }

    private string Output() => _output.ToString().ReplaceLineEndings("\n");

    private static (int Width, int Height) PngSize(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A], file[..8]);
        Assert.Equal("IHDR"u8.ToArray(), file[12..16]);
        return ((int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(16)), (int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(20)));
    }
}
