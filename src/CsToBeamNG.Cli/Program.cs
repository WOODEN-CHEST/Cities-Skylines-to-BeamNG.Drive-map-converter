using System.CommandLine;

namespace CsToBeamNG.Cli;

/// <summary>The entry point of the <c>cstobeamng</c> command-line tool.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // The root command provides --help and --version (the informational version) by itself.
        RootCommand rootCommand = new("Converts a Cities: Skylines city into a drivable BeamNG.drive level.");

        // Without arguments, show the help instead of silently doing nothing.
        string[] effectiveArgs = args.Length == 0 ? ["--help"] : args;
        return rootCommand.Parse(effectiveArgs).Invoke();
    }
}
