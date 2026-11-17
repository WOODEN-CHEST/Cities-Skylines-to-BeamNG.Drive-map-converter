using CsToBeamNG.Dump.Autorun;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// What <see cref="AutorunSequence"/> needs from the game. The game-facing implementation is
/// <c>Autorun/AutorunDriver</c>; tests use a fake. Every member is called on the main thread.
/// </summary>
internal interface IAutorunHost
{
    /// <summary>Whether the main menu is shown and nothing is loading or quitting, so a save can be loaded.</summary>
    bool IsMenuIdle { get; }

    /// <summary>Whether a city is loaded for playing, so it can be exported.</summary>
    bool IsCityLoaded { get; }

    /// <summary>Starts loading a save, as the main menu's Continue does.</summary>
    /// <param name="saveName">The save's name.</param>
    /// <returns><see langword="null"/> if loading started; otherwise why not, for the user.</returns>
    string? StartLoadingSave(string saveName);

    /// <summary>
    /// Starts an export of the loaded city. Its end is reported later through
    /// <see cref="AutorunSequence.OnExportFinished"/>, on the main thread.
    /// </summary>
    void StartExport();

    /// <summary>Publishes a status for the tool that made the request.</summary>
    /// <param name="status">The status.</param>
    void ReportStatus(AutorunStatus status);

    /// <summary>Quits the game the way its Exit button does.</summary>
    void Quit();

    /// <summary>Quits the game at once, for when <see cref="Quit"/> didn't end it.</summary>
    void ForceQuit();

    /// <summary>Logs progress.</summary>
    /// <param name="message">The message.</param>
    void Log(string message);

    /// <summary>Logs a problem.</summary>
    /// <param name="message">The message.</param>
    void Warn(string message);
}
