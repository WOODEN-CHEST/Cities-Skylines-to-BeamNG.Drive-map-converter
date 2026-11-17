using System;
using System.CommandLine;
using System.Globalization;
using System.IO;
using CsToBeamNG.Converter.BeamNG.Install;
using CsToBeamNG.Converter.Development;
using CsToBeamNG.Converter.Discovery;

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
        return command;
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
