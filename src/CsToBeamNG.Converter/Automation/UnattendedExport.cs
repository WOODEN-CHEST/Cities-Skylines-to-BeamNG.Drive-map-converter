using System;
using System.Globalization;
using System.IO;
using CsToBeamNG.Dump.Autorun;

namespace CsToBeamNG.Converter.Automation;

/// <summary>
/// Exports a save without anyone clicking: writes an autorun request, starts the game, follows the
/// exporter's status until it is final, and waits for the game to quit (docs/architecture.md, Unattended
/// export).
/// </summary>
public static class UnattendedExport
{
    /// <summary>How often the status file is read.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>How long the game may take to quit after the final status.</summary>
    public static readonly TimeSpan ExitTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Runs an export. Problems are returned as a failed result, not thrown.</summary>
    /// <param name="options">What to export.</param>
    /// <param name="launcher">Starts the game.</param>
    /// <param name="clock">The time.</param>
    /// <param name="progress">Receives progress messages for the user.</param>
    public static UnattendedExportResult Run(
        UnattendedExportOptions options,
        IGameLauncher launcher,
        IWaitClock clock,
        Action<string> progress)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(progress);

        if (launcher.IsGameRunning())
        {
            return Failure("Cities: Skylines is already running. Quit it first: the exporter takes requests only when the game starts.");
        }

        if (!launcher.IsSteamRunning())
        {
            return Failure("Steam isn't running. Start it first: Cities: Skylines needs it.");
        }

        string folder = AutorunFiles.GetFolder(options.UserDataDir);
        string requestPath = Path.Join(folder, AutorunFiles.RequestFileName);
        string statusPath = Path.Join(folder, AutorunFiles.StatusFileName);
        var request = new AutorunRequest(Guid.NewGuid().ToString("N"), clock.UtcNow, options.SaveName, quitWhenDone: !options.KeepRunning);
        try
        {
            Directory.CreateDirectory(folder);
            AutorunFiles.Delete(statusPath);
            AutorunFiles.WriteText(requestPath, request.ToText());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Failure($"The request couldn't be written to '{requestPath}': {e.Message}");
        }

        progress($"Exporting the save \"{options.SaveName}\". The game starts, loads it, exports it and "
            + (options.KeepRunning ? "keeps running." : "quits by itself."));
        IGameProcess process;
        try
        {
            process = launcher.Start();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            RemoveRequest(requestPath);
            return Failure($"Cities: Skylines couldn't be started: {e.Message}");
        }

        using (process)
        {
            progress($"Started Cities: Skylines (process {process.Id.ToString(CultureInfo.InvariantCulture)}); waiting for the exporter.");
            return Follow(options, request, process, requestPath, statusPath, clock, progress);
        }
    }

    private static UnattendedExportResult Follow(
        UnattendedExportOptions options,
        AutorunRequest request,
        IGameProcess process,
        string requestPath,
        string statusPath,
        IWaitClock clock,
        Action<string> progress)
    {
        DateTime start = clock.UtcNow;
        AutorunState? lastState = null;
        while (true)
        {
            // Read the status before checking for exit: a game that quits after its final status must not
            // count as one that quit early.
            bool exited = process.HasExited;
            AutorunStatus? status = ReadStatus(statusPath, request.Id);
            if (status is not null && status.State != lastState)
            {
                lastState = status.State;
                progress($"[{Elapsed(clock, start)}] {Describe(status)}");
            }

            if (status is { IsFinal: true })
            {
                return Finish(options, status, process, clock, progress);
            }

            if (exited)
            {
                return ExitedEarly(options, process, requestPath, clock, start);
            }

            if (clock.UtcNow - start > options.Timeout)
            {
                RemoveRequest(requestPath);
                string what = lastState is null ? "the exporter never reported" : $"the last state was {AutorunStatus.GetStateName(lastState.Value)}";
                if (options.KeepRunning)
                {
                    return Failure($"No result after {Minutes(options.Timeout)}; {what}. The game was left running.");
                }

                process.Kill();
                return Failure($"No result after {Minutes(options.Timeout)}; {what}. The game was stopped.{SeeLog(options)}");
            }

            clock.Wait(PollInterval);
        }
    }

    private static UnattendedExportResult Finish(
        UnattendedExportOptions options,
        AutorunStatus status,
        IGameProcess process,
        IWaitClock clock,
        Action<string> progress)
    {
        bool succeeded = status.State == AutorunState.Succeeded;
        string message = status.Message ?? (succeeded ? "The export succeeded." : "The export failed.");
        if (!options.KeepRunning)
        {
            DateTime quitStart = clock.UtcNow;
            while (!process.HasExited && clock.UtcNow - quitStart <= ExitTimeout)
            {
                clock.Wait(PollInterval);
            }

            progress(process.HasExited
                ? "Cities: Skylines has quit."
                : $"Cities: Skylines hasn't quit after {Minutes(ExitTimeout)}; it was left running.");
        }

        return new UnattendedExportResult(succeeded, succeeded ? message : message + SeeLog(options), status.DumpFolder);
    }

    private static UnattendedExportResult ExitedEarly(
        UnattendedExportOptions options,
        IGameProcess process,
        string requestPath,
        IWaitClock clock,
        DateTime start)
    {
        bool taken = !File.Exists(requestPath);
        RemoveRequest(requestPath);
        string exit = process.ExitCode is { } code ? $" with exit code {code.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        string after = Elapsed(clock, start);
        return taken
            ? Failure($"Cities: Skylines exited{exit} after {after}, before the export finished.{SeeLog(options)}")
            : Failure($"Cities: Skylines exited{exit} after {after} without taking the request. Check that the exporter mod is "
                + $"enabled (Content Manager > Mods) and that the game can start outside Steam's launcher.{SeeLog(options)}");
    }

    // A status of another request, or a file caught while it is being replaced, is not this request's.
    private static AutorunStatus? ReadStatus(string statusPath, string requestId)
    {
        try
        {
            string? text = AutorunFiles.ReadText(statusPath);
            if (text is null)
            {
                return null;
            }

            AutorunStatus status = AutorunStatus.Parse(text);
            return status.RequestId == requestId ? status : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    // A request that the game didn't take must not surprise the user at the next start of the game. The
    // exporter would ignore it once it is stale, but removing it is clearer.
    private static void RemoveRequest(string requestPath)
    {
        try
        {
            AutorunFiles.Delete(requestPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The exporter ignores stale requests (AutorunRequest.MaxAge).
        }
    }

    private static string Describe(AutorunStatus status) => status.State switch
    {
        AutorunState.Accepted => "The exporter took the request. " + status.Message,
        AutorunState.Succeeded => "Done. " + status.Message,
        AutorunState.Failed => "Failed. " + status.Message,
        _ => status.Message ?? AutorunStatus.GetStateName(status.State),
    };

    private static string Elapsed(IWaitClock clock, DateTime start) =>
        ((int)(clock.UtcNow - start).TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";

    private static string Minutes(TimeSpan duration) =>
        duration.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture) + " min";

    private static string SeeLog(UnattendedExportOptions options) =>
        options.GameLogPath is null ? string.Empty : $" The game log is '{options.GameLogPath}'.";

    private static UnattendedExportResult Failure(string message) => new(false, message, null);
}
