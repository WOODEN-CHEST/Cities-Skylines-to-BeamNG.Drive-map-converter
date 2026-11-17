using System;
using System.IO;
using CsToBeamNG.Converter.Automation;
using CsToBeamNG.Converter.Discovery;
using CsToBeamNG.Dump.Autorun;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class ExportCommandTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void PrintsTheDumpFolderAfterASuccessfulExport()
    {
        var game = new ScriptedGame(_temp.PathOf("user"), succeed: true);

        int exitCode = Export(Environment(), "citypalma", game);

        Assert.Equal(0, exitCode);
        Assert.Empty(_error.ToString());
        string output = _output.ToString().ReplaceLineEndings("\n");
        Assert.StartsWith("Exporting the save \"citypalma\". The game starts, loads it, exports it and quits by itself.\n", output, StringComparison.Ordinal);
        Assert.EndsWith("Dump: /dumps/san-palma\n", output, StringComparison.Ordinal);
        Assert.Equal("citypalma", game.RequestedSave);
    }

    [Fact]
    public void ReportsAFailedExportWithItsIncompleteDump()
    {
        var game = new ScriptedGame(_temp.PathOf("user"), succeed: false);

        int exitCode = Export(Environment(), "CityPalma", game);

        Assert.Equal(ExportCommand.FailedExitCode, exitCode);
        Assert.Equal(
            "An incomplete dump may be in '/dumps/partial'.\nerror: The export failed: disk full\n",
            _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ListsTheSavesWhenTheNameIsUnknown()
    {
        int exitCode = Export(Environment(), "Palma", game: null);

        Assert.Equal(ExportCommand.FailedExitCode, exitCode);
        Assert.Equal("error: There is no save named \"Palma\". The saves are: CityPalma, Rockville.\n", _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void NeedsTheExporterMod()
    {
        GameEnvironment environment = Environment();
        environment = environment with { CitiesSkylines = environment.CitiesSkylines with { ExporterModVersion = null } };

        int exitCode = Export(environment, "CityPalma", game: null);

        Assert.Equal(ExportCommand.FailedExitCode, exitCode);
        Assert.StartsWith("error: The exporter mod isn't installed", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsTheGame()
    {
        GameEnvironment environment = Environment();
        environment = environment with { CitiesSkylines = environment.CitiesSkylines with { InstallDir = null } };

        int exitCode = Export(environment, "CityPalma", game: null);

        Assert.Equal(ExportCommand.FailedExitCode, exitCode);
        Assert.StartsWith("error: Cities: Skylines wasn't found.", _error.ToString(), StringComparison.Ordinal);
    }

    private int Export(GameEnvironment environment, string save, ScriptedGame? game) =>
        ExportCommand.Export(
            environment,
            save,
            TimeSpan.FromMinutes(1),
            keepRunning: false,
            _ => game ?? throw new InvalidOperationException("The game must not be started."),
            game ?? (IWaitClock)new NoClock(),
            _output,
            _error);

    private GameEnvironment Environment()
    {
        string userDir = Directory.CreateDirectory(_temp.PathOf("user")).FullName;
        string installDir = Directory.CreateDirectory(_temp.PathOf("game")).FullName;
        return new GameEnvironment
        {
            Os = OsKind.Linux,
            Steam = new SteamInfo([], []),
            CitiesSkylines = new CitiesSkylinesInfo
            {
                InstallDir = new FoundPath(installDir, LocationSource.Override, Exists: true),
                UserDir = new FoundPath(userDir, LocationSource.Override, Exists: true),
                ExporterModVersion = "0.1.0.0",
                Saves =
                [
                    new SaveGame("Rockville", "/saves/Rockville.crp", 1, DateTime.UnixEpoch, SaveLocation.Local, null),
                    new SaveGame("CityPalma", "/cloud/CityPalma.crp", 1, DateTime.UnixEpoch, SaveLocation.SteamCloud, "1"),
                ],
            },
            BeamNG = new BeamNGInfo(),
            Problems = [],
        };
    }

    /// <summary>Takes the request at once and reports success or failure, then exits.</summary>
    private sealed class ScriptedGame(string userDataDir, bool succeed) : IGameLauncher, IGameProcess, IWaitClock
    {
        private readonly string _folder = AutorunFiles.GetFolder(userDataDir);

        public string? RequestedSave { get; private set; }

        public int Id => 1;

        public bool HasExited { get; private set; }

        public int? ExitCode => 0;

        public DateTime UtcNow { get; private set; } = DateTime.UnixEpoch;

        public bool IsGameRunning() => false;

        public bool IsSteamRunning() => true;

        public IGameProcess Start() => this;

        public void Wait(TimeSpan duration)
        {
            UtcNow += duration;
            string requestPath = Path.Join(_folder, AutorunFiles.RequestFileName);
            if (RequestedSave is null && File.Exists(requestPath))
            {
                AutorunRequest request = AutorunRequest.Parse(File.ReadAllText(requestPath));
                File.Delete(requestPath);
                RequestedSave = request.SaveName;
                var status = succeed
                    ? new AutorunStatus(request.Id, AutorunState.Succeeded, "Exported.", "/dumps/san-palma")
                    : new AutorunStatus(request.Id, AutorunState.Failed, "The export failed: disk full", "/dumps/partial");
                AutorunFiles.WriteText(Path.Join(_folder, AutorunFiles.StatusFileName), status.ToText());
            }
            else
            {
                HasExited = true;
            }
        }

        public void Kill() => HasExited = true;

        public void Dispose()
        {
        }
    }

    private sealed class NoClock : IWaitClock
    {
        public DateTime UtcNow => DateTime.UnixEpoch;

        public void Wait(TimeSpan duration) => throw new InvalidOperationException("Nothing should wait.");
    }
}
