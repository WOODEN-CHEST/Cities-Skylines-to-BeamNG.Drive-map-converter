using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Linq;
using CsToBeamNG.Converter.Automation;
using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Cli;

/// <summary><c>cstobeamng export</c>: exports a save into a city dump without anyone clicking in the game.</summary>
internal static class ExportCommand
{
    /// <summary>The exit code when the export failed or couldn't start.</summary>
    public const int FailedExitCode = 1;

    private const int DefaultTimeoutMinutes = 30;

    private static readonly string[] _knownProbes = [DumpProbes.Geometry, DumpProbes.Heights];

    public static Command Create()
    {
        var locations = LocationOptions.ForCitiesSkylines();
        var save = new Argument<string>("save")
        {
            Description = "The save to export, as the game's Load panel lists it: normally its file name without .crp. `cstobeamng doctor` lists the saves.",
        };
        var timeout = new Option<int>("--timeout")
        {
            Description = "How many minutes to wait for the export, from the game's start.",
            HelpName = "minutes",
            DefaultValueFactory = _ => DefaultTimeoutMinutes,
        };
        timeout.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() <= 0)
            {
                result.AddError("--timeout must be at least 1 minute.");
            }
        });
        var keepRunning = new Option<bool>("--keep-running")
        {
            Description = "Leave the game running after the export instead of quitting it.",
        };
        var probes = new Option<string[]>("--probe")
        {
            Description = "For development: also run a probe and write it into the dump's probes folder. "
                + "Known probes: " + DumpProbes.Geometry + " (road meshes and how the game bends them; `cstobeamng dev geometry-probe` checks it), "
                + DumpProbes.Heights + " (the game's terrain heights at roads, buildings and test points; `cstobeamng dev height-probe` checks it). Repeatable.",
            HelpName = "name",
        };
        probes.Validators.Add(result =>
        {
            foreach (string probe in result.GetValueOrDefault<string[]>() ?? [])
            {
                if (!_knownProbes.Contains(probe))
                {
                    result.AddError($"Unknown probe '{probe}'. Known probes: {string.Join(", ", _knownProbes)}.");
                }
            }
        });
        var command = new Command(
            "export",
            "Starts Cities: Skylines, loads a save, exports it into a city dump and quits the game, without anyone clicking. "
            + "Steam must be running and the exporter mod enabled. Prints the dump's folder.");
        command.Arguments.Add(save);
        command.Options.Add(timeout);
        command.Options.Add(keepRunning);
        command.Options.Add(probes);
        locations.AddTo(command);
        command.SetAction(parseResult =>
        {
            GameEnvironment environment = GameEnvironmentLocator.Locate(DiscoveryContext.ForCurrentProcess(locations.GetOverrides(parseResult)));
            return Export(
                environment,
                parseResult.GetRequiredValue(save),
                TimeSpan.FromMinutes(parseResult.GetValue(timeout)),
                parseResult.GetValue(keepRunning),
                installDir => new CitiesSkylinesLauncher(installDir, environment.Os),
                SystemWaitClock.Instance,
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error,
                parseResult.GetValue(probes) ?? []);
        });
        return command;
    }

    internal static int Export(
        GameEnvironment environment,
        string saveName,
        TimeSpan timeout,
        bool keepRunning,
        Func<string, IGameLauncher> createLauncher,
        IWaitClock clock,
        TextWriter output,
        TextWriter error,
        IReadOnlyList<string>? probes = null)
    {
        CitiesSkylinesInfo game = environment.CitiesSkylines;
        if (game.InstallDir is not { Exists: true } installDir)
        {
            return Fail(error, "Cities: Skylines wasn't found. Pass --cs1-dir; `cstobeamng doctor` shows where it was looked for.");
        }

        if (game.UserDir is not { Exists: true } userDir)
        {
            return Fail(error, "Cities: Skylines' user data folder wasn't found. Start the game once, or pass --cs1-user-dir.");
        }

        if (game.ExporterModVersion is null)
        {
            return Fail(error, "The exporter mod isn't installed in the game's mods folder. Build the solution where the game is installed; that deploys it.");
        }

        if (game.Saves.Count > 0 && !game.Saves.Any(s => string.Equals(s.Name, saveName, StringComparison.OrdinalIgnoreCase)))
        {
            string known = string.Join(", ", game.Saves.Select(s => s.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase));
            return Fail(error, $"There is no save named \"{saveName}\". The saves are: {known}.");
        }

        var options = new UnattendedExportOptions
        {
            UserDataDir = userDir.Path,
            SaveName = saveName,
            Timeout = timeout,
            KeepRunning = keepRunning,
            GameLogPath = game.LogFile?.Path,
            Probes = probes ?? [],
        };
        UnattendedExportResult result = UnattendedExport.Run(options, createLauncher(installDir.Path), clock, message => output.WriteLine(message));
        if (!result.Succeeded)
        {
            if (result.DumpFolder is not null)
            {
                error.WriteLine($"An incomplete dump may be in '{result.DumpFolder}'.");
            }

            return Fail(error, result.Message);
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Dump: {result.DumpFolder}"));
        return 0;
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine("error: " + message);
        return FailedExitCode;
    }
}
