using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Writes <c>[CsToBeamNG]</c> lines to the game log (agents/environment.md has its location).
/// </summary>
/// <remarks>
/// Problems are logged as warnings, never as errors: the game collects logged errors and exceptions for
/// its own error display (docs/research/cs1-save-and-world.md, Runtime and modding facts), and the
/// exporter tells the user about its failures itself. Unity's logging works from any thread; the game
/// itself logs from its simulation thread.
/// </remarks>
internal static class GameLog
{
    private const string Prefix = "[CsToBeamNG] ";

    /// <summary>Logs progress or a result.</summary>
    /// <param name="message">The message.</param>
    public static void Info(string message) => Debug.Log(Prefix + message);

    /// <summary>Logs a problem.</summary>
    /// <param name="message">The message.</param>
    public static void Warning(string message) => Debug.LogWarning(Prefix + message);

    /// <summary>Receives the lines that <see cref="ExportLog"/> passes on.</summary>
    /// <param name="level">The line's level.</param>
    /// <param name="message">The message.</param>
    public static void Forward(ExportLogLevel level, string message)
    {
        switch (level)
        {
            case ExportLogLevel.Warning:
                Warning(message);
                break;
            case ExportLogLevel.Error:
                Warning("Error: " + message);
                break;
            default:
                Info(message);
                break;
        }
    }
}
