using System;
using System.Collections.Generic;
using System.IO;
using CsToBeamNG.Converter.Automation;
using CsToBeamNG.Dump.Autorun;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Automation;

public sealed class UnattendedExportTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly FakeClock _clock = new();
    private readonly List<string> _progress = [];

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FollowsTheExporterUntilTheGameHasQuit()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(3, g => g.Report(AutorunState.LoadingSave, "Loading the save \"CityPalma\"."))
            .At(5, g => g.Report(AutorunState.Exporting, "Exporting the city."))
            .At(6, g => g.Report(AutorunState.Succeeded, "Exported \"San Palma\".", "/dumps/san-palma"))
            .At(9, g => g.Exit());

        UnattendedExportResult result = Run(game);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Exported \"San Palma\".", result.Message);
        Assert.Equal("/dumps/san-palma", result.DumpFolder);
        Assert.True(game.HasExited);
        Assert.False(game.Killed);
        Assert.Equal(
            [
                "Exporting the save \"CityPalma\". The game starts, loads it, exports it and quits by itself.",
                "Started Cities: Skylines (process 4242); waiting for the exporter.",
                "[0 s] The exporter took the request. Waiting for the main menu.",
                "[1 s] Loading the save \"CityPalma\".",
                "[2 s] Exporting the city.",
                "[3 s] Done. Exported \"San Palma\".",
                "Cities: Skylines has quit.",
            ],
            _progress);
        AutorunRequest request = AutorunRequest.Parse(game.TakenRequestText!);
        Assert.Equal("CityPalma", request.SaveName);
        Assert.True(request.QuitWhenDone);
        Assert.Equal(new DateTime(2026, 11, 17, 16, 0, 0, DateTimeKind.Utc), request.CreatedUtc);
        Assert.False(File.Exists(game.RequestPath));
    }

    [Fact]
    public void LeavesTheGameRunningWhenAsked()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(2, g => g.Report(AutorunState.Succeeded, "Exported.", "/dumps/x"));

        UnattendedExportResult result = Run(game, keepRunning: true);

        Assert.True(result.Succeeded);
        Assert.False(game.HasExited);
        Assert.False(AutorunRequest.Parse(game.TakenRequestText!).QuitWhenDone);
        Assert.DoesNotContain(_progress, line => line.Contains("quit", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsAFailureFromTheExporter()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(2, g => g.Report(AutorunState.Failed, "The game has no save named \"CityPalma\"."))
            .At(3, g => g.Exit());

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.Equal("The game has no save named \"CityPalma\". The game log is '/logs/Player.log'.", result.Message);
        Assert.Contains("[1 s] Failed. The game has no save named \"CityPalma\".", _progress);
    }

    [Fact]
    public void RefusesWhenTheGameIsAlreadyRunning()
    {
        FakeGame game = NewGame();
        game.GameAlreadyRunning = true;

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Cities: Skylines is already running.", result.Message, StringComparison.Ordinal);
        Assert.False(game.Started);
        Assert.False(File.Exists(game.RequestPath));
        Assert.Empty(_progress);
    }

    [Fact]
    public void RefusesWhenSteamIsntRunning()
    {
        FakeGame game = NewGame();
        game.SteamRunning = false;

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Steam isn't running.", result.Message, StringComparison.Ordinal);
        Assert.False(game.Started);
    }

    [Fact]
    public void RemovesTheRequestWhenTheGameDoesntStart()
    {
        FakeGame game = NewGame();
        game.StartFailure = new FileNotFoundException("The game's executable '/games/Cities.x64' doesn't exist.");

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.Equal("Cities: Skylines couldn't be started: The game's executable '/games/Cities.x64' doesn't exist.", result.Message);
        Assert.False(File.Exists(game.RequestPath));
    }

    [Fact]
    public void ExplainsAGameThatExitsWithoutTakingTheRequest()
    {
        FakeGame game = NewGame().At(4, g => g.Exit());

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Cities: Skylines exited with exit code 0 after 2 s without taking the request.", result.Message, StringComparison.Ordinal);
        Assert.Contains("enabled (Content Manager > Mods)", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(game.RequestPath));
    }

    [Fact]
    public void ExplainsAGameThatExitsBeforeTheExportFinished()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(2, g => g.Report(AutorunState.LoadingSave, "Loading."))
            .At(3, g => g.Exit());

        UnattendedExportResult result = Run(game);

        Assert.False(result.Succeeded);
        Assert.Equal("Cities: Skylines exited with exit code 0 after 1 s, before the export finished. The game log is '/logs/Player.log'.", result.Message);
    }

    [Fact]
    public void AcceptsAFinalStatusWrittenJustBeforeTheGameExits()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(2, g =>
            {
                g.Report(AutorunState.Succeeded, "Exported.", "/dumps/x");
                g.Exit();
            });

        UnattendedExportResult result = Run(game);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public void IgnoresStatusesOfOtherRequests()
    {
        FakeGame game = NewGame();
        Directory.CreateDirectory(Path.GetDirectoryName(game.StatusPath)!);
        File.WriteAllText(game.StatusPath, new AutorunStatus("old", AutorunState.Succeeded, "Old.", "/dumps/old").ToText());
        game.At(1, g => g.TakeRequest())
            .At(2, g => g.Report(AutorunState.Succeeded, "Someone else's.", "/dumps/other", requestId: "other"));

        UnattendedExportResult result = Run(game, timeout: TimeSpan.FromSeconds(10));

        Assert.False(result.Succeeded);
        Assert.StartsWith("No result after 0.2 min; the last state was accepted. The game was stopped.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StopsTheGameAfterTheTimeout()
    {
        FakeGame game = NewGame();

        UnattendedExportResult result = Run(game, timeout: TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal("No result after 1 min; the exporter never reported. The game was stopped. The game log is '/logs/Player.log'.", result.Message);
        Assert.True(game.Killed);
        Assert.False(File.Exists(game.RequestPath));
    }

    [Fact]
    public void LeavesTheGameRunningAfterTheTimeoutWhenAsked()
    {
        FakeGame game = NewGame().At(1, g => g.TakeRequest());

        UnattendedExportResult result = Run(game, timeout: TimeSpan.FromMinutes(1), keepRunning: true);

        Assert.False(result.Succeeded);
        Assert.Equal("No result after 1 min; the last state was accepted. The game was left running.", result.Message);
        Assert.False(game.Killed);
    }

    [Fact]
    public void SaysSoWhenTheGameDoesntQuit()
    {
        FakeGame game = NewGame()
            .At(1, g => g.TakeRequest())
            .At(2, g => g.Report(AutorunState.Succeeded, "Exported.", "/dumps/x"));

        UnattendedExportResult result = Run(game);

        Assert.True(result.Succeeded);
        Assert.Equal("Cities: Skylines hasn't quit after 2 min; it was left running.", _progress[^1]);
        Assert.False(game.Killed);
    }

    private FakeGame NewGame() => new(_temp.PathOf("user"), _clock);

    private UnattendedExportResult Run(FakeGame game, TimeSpan? timeout = null, bool keepRunning = false) =>
        UnattendedExport.Run(
            new UnattendedExportOptions
            {
                UserDataDir = _temp.PathOf("user"),
                SaveName = "CityPalma",
                Timeout = timeout ?? UnattendedExportOptions.DefaultTimeout,
                KeepRunning = keepRunning,
                GameLogPath = "/logs/Player.log",
            },
            game,
            _clock,
            _progress.Add);
}
