using System;
using System.CommandLine;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Cli;

/// <summary><c>cstobeamng doctor</c>: shows what discovery found.</summary>
internal static class DoctorCommand
{
    /// <summary>The exit code when something essential is missing.</summary>
    public const int EssentialsMissingExitCode = 1;

    public static Command Create()
    {
        var locations = new LocationOptions();
        var command = new Command(
            "doctor",
            "Shows where Cities: Skylines, BeamNG.drive and their folders were found, and any problems. "
            + "Exits with code 1 if something essential is missing.");
        locations.AddTo(command);
        command.SetAction(parseResult =>
        {
            DiscoveryContext context = DiscoveryContext.ForCurrentProcess(locations.GetOverrides(parseResult));
            GameEnvironment environment = GameEnvironmentLocator.Locate(context);
            DoctorReport.Write(environment, parseResult.InvocationConfiguration.Output, TimeZoneInfo.Local);
            return environment.HasErrors ? EssentialsMissingExitCode : 0;
        });
        return command;
    }
}
