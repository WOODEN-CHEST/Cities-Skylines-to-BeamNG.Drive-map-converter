using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG.Install;
using CsToBeamNG.Converter.Conversion;
using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Cli;

/// <summary><c>cstobeamng convert</c>: converts a city dump into a BeamNG.drive level and installs it.</summary>
internal static class ConvertCommand
{
    /// <summary>The exit code when the dump couldn't be read or converted, or the level couldn't be installed.</summary>
    public const int FailedExitCode = 1;

    public static Command Create()
    {
        var dump = new Argument<string>("dump")
        {
            Description = "The dump folder, or its manifest.json. `cstobeamng doctor` shows where the exporter puts dumps.",
        };
        var name = new Option<string?>("--name")
        {
            Description = "The level's name: lowercase letters a-z, digits and '_'. The level and its mod folder are called "
                + "cstobeamng_<name>. By default the city's name, such as cstobeamng_san_palma.",
            HelpName = "name",
        };
        name.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string?>() is { } value)
            {
                try
                {
                    LevelNames.FromUserName(value);
                }
                catch (ArgumentException e)
                {
                    result.AddError(e.Message);
                }
            }
        });
        var area = new Option<RenderArea>("--area")
        {
            Description = "The part of the map to convert: aoi (the area of interest, the default), map (the whole map), "
                + "a block of tiles such as 3-5,4, or minX,minZ,maxX,maxZ in metres in the game's coordinates.",
            HelpName = "area",
            DefaultValueFactory = _ => RenderArea.AreaOfInterest,
            CustomParser = ParseArea,
        };
        var maxTerrainSize = new Option<int>("--max-terrain-size")
        {
            Description = "The most terrain samples per side: a power of two from 256 to 8192. Smaller terrains load faster "
                + "and use less memory; the planner then takes coarser squares (1, 2 or 4 m).",
            HelpName = "samples",
            DefaultValueFactory = _ => TerrainGridOptions.Default.MaxSize,
        };
        maxTerrainSize.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value < 256 || value > TerrainGridOptions.Default.MaxSize || !BitOperations.IsPow2(value))
            {
                result.AddError($"--max-terrain-size must be a power of two from 256 to {TerrainGridOptions.Default.MaxSize}.");
            }
        });
        var locations = LocationOptions.ForBeamNGUserDir();
        var command = new Command(
            "convert",
            "Converts a city dump into a BeamNG.drive level and installs it into the game's mods/unpacked folder as "
            + "cstobeamng_<name>, replacing only that level's previous install. The level has the city's terrain, sea and "
            + "lakes so far. A report goes into the level folder. Exits with 1 if the dump can't be read or converted.");
        command.Arguments.Add(dump);
        command.Options.Add(name);
        command.Options.Add(area);
        command.Options.Add(maxTerrainSize);
        locations.AddTo(command);
        command.SetAction(parseResult => Convert(
            new ConvertRequest(parseResult.GetRequiredValue(dump))
            {
                Name = parseResult.GetValue(name),
                Area = parseResult.GetValue(area) ?? RenderArea.AreaOfInterest,
                MaxTerrainSize = parseResult.GetValue(maxTerrainSize),
                Locations = locations.GetOverrides(parseResult),
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error));
        return command;
    }

    internal static int Convert(ConvertRequest request, TextWriter output, TextWriter error)
    {
        var clock = Stopwatch.StartNew();
        string folder = request.DumpPath;
        if (File.Exists(folder) && string.Equals(Path.GetFileName(folder), DumpFormat.ManifestFileName, StringComparison.Ordinal))
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(folder))!;
        }

        string? unpackedModsDir = FindUnpackedModsDir(request.Locations, error);
        if (unpackedModsDir is null)
        {
            return FailedExitCode;
        }

        WorldLoadResult loaded;
        try
        {
            loaded = WorldLoader.Load(folder);
        }
        catch (DumpException e)
        {
            error.WriteLine("error: " + e.Message);
            return FailedExitCode;
        }

        string levelName = request.Name is null ? LevelNames.FromCityName(loaded.World.Manifest.City.Name) : LevelNames.FromUserName(request.Name);
        var options = new ConversionOptions
        {
            LevelName = levelName,
            Area = request.Area.Bounds,
            AreaDescription = request.Area.Bounds is null ? null : request.Area.Description + ", chosen with --area",
            TerrainGrid = TerrainGridOptions.Default with { MaxSize = request.MaxTerrainSize },
        };
        output.WriteLine($"Converting {loaded.World.Manifest.City.Name} ({Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)))}) into the level {levelName}.");
        ConversionResult result;
        try
        {
            result = CityLevelConverter.Convert(loaded, options, stage => output.WriteLine($"  {stage}..."));
        }
        catch (ConversionException e)
        {
            error.WriteLine("error: " + e.Message);
            return FailedExitCode;
        }

        WriteSummary(result.Report, output);
        output.WriteLine("  Writing the level...");
        InstallResult installed;
        try
        {
            installed = ModInstaller.Install(unpackedModsDir, result.LevelName, result.Files);
        }
        catch (InstallException e)
        {
            error.WriteLine("error: " + e.Message);
            return FailedExitCode;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: writing the level failed: {e.Message}");
            return FailedExitCode;
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Installed the level {result.LevelName}: {installed.FileCount} files, {DoctorReport.FormatSize(installed.ByteCount)}, in {clock.Elapsed.TotalSeconds:0.0} s."));
        output.WriteLine($"  Folder: {installed.ModDir}");
        output.WriteLine($"  Report: {Path.Join(installed.ModDir, "levels", result.LevelName, ConversionReport.FileName)}");
        if (installed.ReplacedFileCount > 0 || installed.RemovedFileCount > 0)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  Updated the previous install: {installed.ReplacedFileCount} files replaced, {installed.RemovedFileCount} no longer needed removed."));
        }

        output.WriteLine("In BeamNG.drive, open Freeroam and choose the level. If the game was running, it may need a restart to list it.");
        return 0;
    }

    private static string? FindUnpackedModsDir(DiscoveryOverrides overrides, TextWriter error)
    {
        GameEnvironment environment = GameEnvironmentLocator.Locate(DiscoveryContext.ForCurrentProcess(overrides));
        FoundPath? userDir = environment.BeamNG.UserDir;
        if (userDir is null || !userDir.Exists)
        {
            error.WriteLine(
                "error: BeamNG.drive's user folder wasn't found. Start the game once, or pass --beamng-user-dir. "
                + "`cstobeamng doctor` shows where it was looked for.");
            return null;
        }

        return environment.BeamNG.UnpackedModsDir?.Path ?? Path.Join(userDir.Path, "mods", "unpacked");
    }

    private static void WriteSummary(ConversionReport report, TextWriter output)
    {
        TerrainGrid grid = report.Grid;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  Area: {report.Area} ({report.Area.Width:0} × {report.Area.Depth:0} m)"));
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  Terrain: {grid.Size} × {grid.Size} samples, {grid.SquareSize:0.###} m apart, {grid.ExtentMeters:0} m across; heights {report.LowestHeight:0.##} to {report.HighestHeight:0.##} m"));
        long total = 0;
        foreach (long count in report.SurfaceCounts)
        {
            total += count;
        }

        var surfaces = new System.Collections.Generic.List<string>();
        foreach (SurfaceKind kind in Enum.GetValues<SurfaceKind>())
        {
            if (report.SurfaceCounts[(int)kind] > 0)
            {
                surfaces.Add(string.Create(CultureInfo.InvariantCulture, $"{kind.ToString().ToLowerInvariant()} {report.SurfaceCounts[(int)kind] * 100.0 / total:0.#}%"));
            }
        }

        output.WriteLine($"  Materials: {string.Join(", ", surfaces)}");
        if (report.Water is WaterBodies water)
        {
            string sea = water.SeaSurface is double level ? string.Create(CultureInfo.InvariantCulture, $"sea at {level:0.##} m") : "no sea";
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  Water: {sea}; {water.Lakes.Count} {(water.Lakes.Count == 1 ? "lake" : "lakes")}; {water.SkippedBodies} {(water.SkippedBodies == 1 ? "river" : "rivers")} left out ({water.SkippedCells * water.CellAreaSquareMeters / 1e6:0.##} km²)"));
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  Spawn point: ({report.SpawnPosition.X:0.#}, {report.SpawnPosition.Y:0.#}, {report.SpawnPosition.Z:0.#})"));
        foreach (string note in report.Notes)
        {
            output.WriteLine("  " + note);
        }
    }

    private static RenderArea? ParseArea(ArgumentResult result)
    {
        if (MapAreaSyntax.TryParseArea(result.Tokens[0].Value, out RenderArea? parsed, out string? message))
        {
            return parsed;
        }

        result.AddError(message);
        return null;
    }
}
