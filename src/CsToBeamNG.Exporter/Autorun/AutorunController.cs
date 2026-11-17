using System;
using System.Globalization;
using System.IO;
using ColossalFramework.IO;
using CsToBeamNG.Dump.Autorun;
using CsToBeamNG.Exporter.Game;

namespace CsToBeamNG.Exporter.Autorun;

/// <summary>
/// Picks up an autorun request when the game enables the mod at startup, and hands it to an
/// <see cref="AutorunDriver"/>. Without a request file it does nothing, which is the normal case
/// (docs/architecture.md, Unattended export).
/// </summary>
internal static class AutorunController
{
    // Main thread only. The game may enable the mod again (Content Manager), but a request is for startup.
    private static bool _checked;

    /// <summary>Called by the mod's <c>OnEnabled</c>, on the main thread.</summary>
    public static void OnModEnabled()
    {
        if (_checked)
        {
            return;
        }

        _checked = true;
        string folder = AutorunFiles.GetFolder(DataLocation.localApplicationData);
        string requestPath = Path.Combine(folder, AutorunFiles.RequestFileName);
        string? text = AutorunFiles.ReadText(requestPath);
        if (text is null)
        {
            return;
        }

        // Take the request before acting on it, so a crash while handling it can't make every later start
        // of the game act on it again.
        if (!TakeRequest(requestPath, Path.Combine(folder, AutorunFiles.ConsumedRequestFileName)))
        {
            return;
        }

        var reporter = new StatusReporter(Path.Combine(folder, AutorunFiles.StatusFileName));
        AutorunRequest request;
        try
        {
            request = AutorunRequest.Parse(text);
        }
        catch (FormatException e)
        {
            Reject(reporter, AutorunRequest.TryReadId(text), "The autorun request " + requestPath + " is invalid: " + e.Message);
            return;
        }

        if (request.IsStale(DateTime.UtcNow))
        {
            Reject(reporter, request.Id, string.Format(
                CultureInfo.InvariantCulture,
                "The autorun request {0} was written at {1:yyyy-MM-dd HH:mm:ss} UTC, more than {2} minutes before the game started, so it was ignored. A tool that requests an export writes it just before starting the game.",
                request.Id,
                request.CreatedUtc,
                AutorunRequest.MaxAge.TotalMinutes));
            return;
        }

        AutorunDriver.Begin(request, reporter);
    }

    private static bool TakeRequest(string requestPath, string consumedPath)
    {
        try
        {
            AutorunFiles.Move(requestPath, consumedPath);
            return true;
        }
        catch (Exception moveError)
        {
            try
            {
                AutorunFiles.Delete(requestPath);
                return true;
            }
            catch (Exception deleteError)
            {
                GameLog.Warning("Autorun: the request " + requestPath + " can't be renamed (" + moveError.Message + ") or deleted ("
                    + deleteError.Message + "), so it is ignored: acting on it could repeat at every start of the game.");
                return false;
            }
        }
    }

    // An unusable request doesn't quit the game: without a valid request, it may be a person who started it.
    private static void Reject(StatusReporter reporter, string? requestId, string message)
    {
        GameLog.Warning("Autorun: " + message);
        reporter.Report(new AutorunStatus(requestId, AutorunState.Failed, message, null));
    }
}
