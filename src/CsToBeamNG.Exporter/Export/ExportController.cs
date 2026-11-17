using System;
using ColossalFramework.IO;
using ColossalFramework.Threading;
using CsToBeamNG.Dump;
using CsToBeamNG.Exporter.Game;
using ICities;

namespace CsToBeamNG.Exporter.Export;

/// <summary>
/// Starts exports and reports how they ended. The game creates the mod, its loading extension and the
/// button's callback separately, so they share this class's state.
/// </summary>
/// <remarks>
/// Threads (docs/research/cs1-save-and-world.md, Threads): the button and the loading events arrive on
/// the main thread. The export itself runs as an action on the simulation thread, which the game runs
/// even while it is paused, such as while Options is open. The outcome goes back to the main thread
/// through the game's dispatcher, because only the main thread may show UI.
/// </remarks>
internal static class ExportController
{
    // Guards _activeJob, which the main thread and the simulation thread both change.
    private static readonly object _lock = new();

    // Main thread only.
    private static bool _cityLoaded;

    private static ExportJob? _activeJob;

    /// <summary>
    /// The folder that receives the dumps: <c>&lt;CS1 user data&gt;/CsToBeamNG/dumps</c>. Ask on the
    /// main thread.
    /// </summary>
    public static string DumpsFolder => DumpLocation.GetDumpsFolder(DataLocation.localApplicationData);

    /// <summary>Called when a level has loaded. Exports are possible in games, not in the editors.</summary>
    /// <param name="mode">How the level was loaded, for the log.</param>
    /// <param name="appMode">What kind of level it is.</param>
    public static void OnLevelLoaded(LoadMode mode, AppMode appMode)
    {
        _cityLoaded = appMode == AppMode.Game;
        GameLog.Info("Level loaded (" + mode + ", " + appMode + "); exporting is " + (_cityLoaded ? "possible." : "only possible in a game."));
    }

    /// <summary>Called when the level is about to unload. An export that hasn't run yet is abandoned.</summary>
    public static void OnLevelUnloading()
    {
        _cityLoaded = false;
        lock (_lock)
        {
            _activeJob?.Abandon();
            _activeJob = null;
        }
    }

    /// <summary>The Export button's callback, on the main thread.</summary>
    public static void OnExportClicked()
    {
        try
        {
            StartExport();
        }
        catch (Exception e)
        {
            // Never let an exception reach the game's UI code.
            lock (_lock)
            {
                _activeJob = null;
            }

            GameLog.Warning("The export couldn't start: " + e);
            ShowMessage("The export couldn't start: " + e.Message + "\n\nDetails are in the game log.", isError: true);
        }
    }

    private static void StartExport()
    {
        if (!_cityLoaded)
        {
            ShowMessage("Load a city first. The exporter works in a running city, not in the main menu or the editors.", isError: false);
            return;
        }

        lock (_lock)
        {
            if (_activeJob is not null)
            {
                ShowMessage("An export is already running. Wait for its message.", isError: false);
                return;
            }
        }

        ExportJob job = ExportJob.Prepare(DumpsFolder);
        lock (_lock)
        {
            _activeJob = job;
        }

        GameLog.Info("Export requested.");
        SimulationManager.instance.AddAction(() => RunOnSimulationThread(job));
    }

    private static void RunOnSimulationThread(ExportJob job)
    {
        try
        {
            ExportOutcome outcome;
            try
            {
                outcome = job.Run();
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_activeJob, job))
                    {
                        _activeJob = null;
                    }
                }
            }

            ThreadHelper.dispatcher.Dispatch(() => ShowMessage(outcome.Message, !outcome.Succeeded));
        }
        catch (Exception e)
        {
            // Run doesn't throw, so this would be a bug; the simulation thread must survive it.
            GameLog.Warning("The export failed unexpectedly: " + e);
        }
    }

    private static void ShowMessage(string message, bool isError)
    {
        try
        {
            MessagePanel.Show(message, isError);
        }
        catch (Exception e)
        {
            GameLog.Warning("A message couldn't be shown (" + message.Replace('\n', ' ') + "): " + e);
        }
    }
}
