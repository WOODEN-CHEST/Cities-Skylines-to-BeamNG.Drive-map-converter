using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CsToBeamNG.Converter.BeamNG.Install;
using CsToBeamNG.Converter.Development;
using CsToBeamNG.Converter.Development.GeometryProbe;
using CsToBeamNG.Converter.Development.HeightProbe;
using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Cli;

/// <summary><c>cstobeamng dev</c>: tools for developing the converter.</summary>
internal static class DevCommand
{
    /// <summary>The exit code when the level couldn't be installed.</summary>
    public const int FailedExitCode = 1;

    public static Command Create()
    {
        var command = new Command("dev", "Tools for developing the converter.");
        command.Subcommands.Add(CreateHelloLevel());
        command.Subcommands.Add(CreateGeometryProbe());
        command.Subcommands.Add(CreateHeightProbe());
        return command;
    }

    private static Command CreateHeightProbe()
    {
        var dump = new Argument<string>("dump")
        {
            Description = "A dump folder exported with `--probe heights`.",
        };
        var command = new Command(
            "height-probe",
            "Checks the converter's reading of a dump's terrain against the game's own height samplers, using a heights probe "
            + "from the exporter (`cstobeamng export <save> --probe heights`), and shows how high roads and buildings stand "
            + "above the ground. Exits with 1 if a height differs by more than 5 mm.");
        command.Arguments.Add(dump);
        command.SetAction(parseResult => CheckHeightProbe(
            parseResult.GetRequiredValue(dump),
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error));
        return command;
    }

    internal static int CheckHeightProbe(string dumpFolder, TextWriter output, TextWriter error)
    {
        string file = Path.Join(dumpFolder, DumpProbes.FolderName, DumpProbes.Heights, DumpProbes.HeightsFileName);
        HeightProbeFile probe;
        CityTerrain? terrain;
        try
        {
            probe = HeightProbeFile.Read(file);
            terrain = WorldLoader.Load(dumpFolder).World.Find<CityTerrain>();
        }
        catch (Exception e) when (e is DumpException or IOException or UnauthorizedAccessException or InvalidDataException or JsonException
            or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            error.WriteLine($"error: the probe in '{dumpFolder}' couldn't be read: {e.Message}");
            return FailedExitCode;
        }

        if (terrain is null)
        {
            error.WriteLine($"error: the dump in '{dumpFolder}' has no terrain the converter could load; `cstobeamng inspect` says why.");
            return FailedExitCode;
        }

        output.WriteLine($"Heights probe from game {probe.GameVersion}: {probe.Samples.Count} positions.");
        output.WriteLine("Where the game's height arrays differed (first minus second):");
        foreach (HeightArrayComparison arrays in probe.Arrays)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {arrays.Name,-20} {arrays.Differing,8} of {arrays.Values,8} values, {arrays.MaxBelowMeters:+0.###;-0.###;0} to {arrays.MaxAboveMeters:+0.###;-0.###;0} m"));
        }

        IReadOnlyList<HeightProbeComparison> comparisons = HeightProbeCheck.Compare(terrain, probe);
        output.WriteLine();
        output.Write(HeightProbeCheck.FormatReport(comparisons, HeightProbeCheck.SummarizeObjects(probe)));
        int failed = comparisons.Count(c => !c.IsMatch);
        output.WriteLine(failed == 0
            ? $"All {comparisons.Count} samplers match within {HeightProbeCheck.Tolerance * 1000:0} mm."
            : $"{failed} of {comparisons.Count} samplers don't match.");
        return failed == 0 ? 0 : FailedExitCode;
    }

    private static Command CreateGeometryProbe()
    {
        var path = new Argument<string>("probe")
        {
            Description = "A dump folder exported with `--probe geometry`, or its probes/geometry/geometry-probe.json.",
        };
        var obj = new Option<string?>("--obj")
        {
            Description = "Also write the reconstructed meshes as Wavefront OBJ files into this folder, one per probed object.",
            HelpName = "folder",
        };
        var command = new Command(
            "geometry-probe",
            "Checks the converter's reimplementation of the game's road-mesh bending against the game's own output, "
            + "using a geometry probe from the exporter (`cstobeamng export <save> --probe geometry`). "
            + "Exits with 1 if a position differs by more than 1 mm.");
        command.Arguments.Add(path);
        command.Options.Add(obj);
        command.SetAction(parseResult => CheckGeometryProbe(
            parseResult.GetRequiredValue(path),
            parseResult.GetValue(obj),
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error));
        return command;
    }

    internal static int CheckGeometryProbe(string path, string? objFolder, TextWriter output, TextWriter error)
    {
        string file = Directory.Exists(path) ? Path.Join(path, DumpProbes.FolderName, DumpProbes.Geometry, DumpProbes.GeometryFileName) : path;
        GeometryProbeFile probe;
        try
        {
            probe = GeometryProbeFile.Read(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException
            or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            error.WriteLine($"error: '{file}' couldn't be read: {e.Message}");
            return FailedExitCode;
        }

        output.WriteLine($"Geometry probe from game {probe.GameVersion}: {probe.Objects.Count} objects.");
        foreach (ProbeObject probed in probe.Objects)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {probed.Label}: {probed.Kind} {probed.Id}, {probed.Info} ({probed.Reason}){(probed.Error is null ? string.Empty : " FAILED in the exporter")}"));
        }

        foreach (ProbeMesh mesh in probe.Meshes.Where(m => m.Type == "unityMesh"))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  mesh {mesh.Id} {mesh.Role} '{mesh.Name}': {mesh.VertexCount} vertices, {(mesh.Readable ? "readable" : "NOT readable")}"));
        }

        IReadOnlyList<ProbeComparison> comparisons = GeometryProbeCheck.Compare(probe);
        output.WriteLine();
        output.Write(GeometryProbeCheck.FormatTable(comparisons));
        IReadOnlyList<(string Name, float MaxDistance, float MeanDistance)> shapes = GeometryProbeCheck.CompareLodWithRenderer(probe);
        if (shapes.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Junction main meshes, level-of-detail inputs vs the renderer's inputs (expected to differ):");
            foreach ((string name, float max, float mean) in shapes)
            {
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {name,-60} max {max,8:0.000} m, mean {mean,8:0.000} m"));
            }
        }

        IReadOnlyList<(string Name, float MaxDifference)> renderer = GeometryProbeCheck.CompareWithRenderer(probe);
        if (renderer.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("The probe's renderer inputs vs the renderer's own instance data (largest element difference):");
            foreach ((string name, float max) in renderer)
            {
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {name,-60} {max,12:0.000000}"));
            }
        }

        if (objFolder is not null)
        {
            WriteObjFiles(probe, objFolder);
            output.WriteLine($"Wrote OBJ files to {objFolder}.");
        }

        int failed = comparisons.Count(c => !c.IsMatch);
        output.WriteLine(failed == 0
            ? $"All {comparisons.Count} comparisons match within {GeometryProbeCheck.PositionTolerance * 1000:0} mm."
            : $"{failed} of {comparisons.Count} comparisons don't match.");
        return failed == 0 ? 0 : FailedExitCode;
    }

    private static void WriteObjFiles(GeometryProbeFile probe, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (ProbeObject probed in probe.Objects)
        {
            var main = new List<(string, NetMeshData)>();
            var lod = new List<(string, NetMeshData)>();
            var renderer = new List<(string, NetMeshData)>();
            foreach (ProbeLayer layer in probed.Layers)
            {
                for (int i = 0; i < layer.Pieces.Count; i++)
                {
                    ProbePiece piece = layer.Pieces[i];
                    string name = string.Create(CultureInfo.InvariantCulture, $"L{layer.Layer}_{i}_{piece.Kind}");
                    if (piece.MainMesh >= 0 && probe.Meshes[piece.MainMesh].Data is { } mainMesh)
                    {
                        main.Add((name, GeometryProbeCheck.Bend(piece, mainMesh)));
                        if (piece.Gpu is not null)
                        {
                            renderer.Add((name, GeometryProbeCheck.BendJunction(piece.Gpu, mainMesh)));
                        }
                    }

                    if (piece.SourceData >= 0 && probe.Meshes[piece.SourceData].Data is { } lodMesh)
                    {
                        lod.Add((name, GeometryProbeCheck.Bend(piece, lodMesh)));
                    }
                }
            }

            WriteObjFile(Path.Join(folder, probed.Label + ".obj"), main);
            WriteObjFile(Path.Join(folder, probed.Label + "-lod.obj"), lod);
            if (renderer.Count > 0)
            {
                WriteObjFile(Path.Join(folder, probed.Label + "-renderer.obj"), renderer);
            }
        }
    }

    private static void WriteObjFile(string path, List<(string, NetMeshData)> meshes)
    {
        using var writer = new StreamWriter(path);
        GeometryProbeCheck.WriteObj(writer, meshes);
    }

    private static Command CreateHelloLevel()
    {
        var locations = LocationOptions.ForBeamNGUserDir();
        var command = new Command(
            "hello-level",
            $"Installs a small generated test level, {HelloLevel.LevelName}, into BeamNG.drive's mods/unpacked folder. "
            + "It replaces only the files of its own previous install.");
        locations.AddTo(command);
        command.SetAction(parseResult => InstallHelloLevel(
            locations.GetOverrides(parseResult),
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error));
        return command;
    }

    internal static int InstallHelloLevel(DiscoveryOverrides overrides, TextWriter output, TextWriter error)
    {
        GameEnvironment environment = GameEnvironmentLocator.Locate(DiscoveryContext.ForCurrentProcess(overrides));
        FoundPath? userDir = environment.BeamNG.UserDir;
        if (userDir is null || !userDir.Exists)
        {
            error.WriteLine(
                "error: BeamNG.drive's user folder wasn't found. Start the game once, or pass --beamng-user-dir. "
                + "`cstobeamng doctor` shows where it was looked for.");
            return FailedExitCode;
        }

        string unpackedModsDir = environment.BeamNG.UnpackedModsDir?.Path ?? Path.Join(userDir.Path, "mods", "unpacked");
        try
        {
            InstallResult result = ModInstaller.Install(unpackedModsDir, HelloLevel.ModFolderName, HelloLevel.CreateFiles());
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Installed the level {HelloLevel.LevelName}: {result.FileCount} files, {DoctorReport.FormatSize(result.ByteCount)}."));
            output.WriteLine($"  Folder: {result.ModDir}");
            if (result.ReplacedFileCount > 0 || result.RemovedFileCount > 0)
            {
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  Updated the previous install: {result.ReplacedFileCount} files replaced, {result.RemovedFileCount} no longer needed removed."));
            }

            output.WriteLine("In BeamNG.drive, open Freeroam and choose the level. If the game was running, it may need a restart to list it.");
            return 0;
        }
        catch (InstallException e)
        {
            error.WriteLine("error: " + e.Message);
            return FailedExitCode;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: writing the level failed: {e.Message} If BeamNG.drive has the level loaded, leave it and try again.");
            return FailedExitCode;
        }
    }
}
