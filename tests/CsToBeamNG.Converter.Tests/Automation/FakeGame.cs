using System;
using System.Collections.Generic;
using System.IO;
using CsToBeamNG.Converter.Automation;
using CsToBeamNG.Dump.Autorun;

namespace CsToBeamNG.Converter.Tests.Automation;

/// <summary>
/// A scripted stand-in for the game and its exporter. Each <see cref="FakeClock.Wait"/> is one tick; the
/// script decides what the "exporter" writes and when the "game" exits.
/// </summary>
internal sealed class FakeGame : IGameLauncher, IGameProcess
{
    private readonly string _autorunFolder;
    private readonly Dictionary<int, Action<FakeGame>> _script = [];
    private int _tick;

    public FakeGame(string userDataDir, FakeClock clock)
    {
        _autorunFolder = AutorunFiles.GetFolder(userDataDir);
        clock.OnWait = Tick;
    }

    public bool GameAlreadyRunning { get; set; }

    public bool SteamRunning { get; set; } = true;

    public Exception? StartFailure { get; set; }

    public bool Started { get; private set; }

    public bool Killed { get; private set; }

    public string? TakenRequestText { get; private set; }

    public int Id => 4242;

    public bool HasExited { get; private set; }

    public int? ExitCode => HasExited ? 0 : null;

    public string RequestPath => Path.Join(_autorunFolder, AutorunFiles.RequestFileName);

    public string StatusPath => Path.Join(_autorunFolder, AutorunFiles.StatusFileName);

    /// <summary>Runs <paramref name="action"/> at the given tick (1 is the first wait after the start).</summary>
    public FakeGame At(int tick, Action<FakeGame> action)
    {
        _script[tick] = _script.TryGetValue(tick, out Action<FakeGame>? existing) ? existing + action : action;
        return this;
    }

    /// <summary>Takes the request as the exporter does, and reports Accepted.</summary>
    public void TakeRequest()
    {
        TakenRequestText = File.ReadAllText(RequestPath);
        AutorunFiles.Move(RequestPath, Path.Join(_autorunFolder, AutorunFiles.ConsumedRequestFileName));
        Report(AutorunState.Accepted, "Waiting for the main menu.");
    }

    public void Report(AutorunState state, string? message, string? dump = null, string? requestId = null)
    {
        requestId ??= AutorunRequest.Parse(TakenRequestText!).Id;
        AutorunFiles.WriteText(StatusPath, new AutorunStatus(requestId, state, message, dump).ToText());
    }

    public void Exit() => HasExited = true;

    public bool IsGameRunning() => GameAlreadyRunning;

    public bool IsSteamRunning() => SteamRunning;

    public IGameProcess Start()
    {
        if (StartFailure is not null)
        {
            throw StartFailure;
        }

        Started = true;
        return this;
    }

    public void Kill()
    {
        Killed = true;
        HasExited = true;
    }

    public void Dispose()
    {
    }

    private void Tick()
    {
        _tick++;
        if (Started && _script.TryGetValue(_tick, out Action<FakeGame>? action))
        {
            action(this);
        }
    }
}

/// <summary>A clock whose waits take no time and drive a <see cref="FakeGame"/>.</summary>
internal sealed class FakeClock : IWaitClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 11, 17, 16, 0, 0, DateTimeKind.Utc);

    public Action? OnWait { get; set; }

    public void Wait(TimeSpan duration)
    {
        UtcNow += duration;
        OnWait?.Invoke();
    }
}
