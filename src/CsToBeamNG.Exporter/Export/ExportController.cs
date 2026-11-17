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
/// Threads (docs/research/cs1-save-and-world.md, Threads): the button, the loading events and autorun
/// arrive on the main thread. The export itself runs as an action on the simulation thread, which the
/// game runs even while it is paused, such as while Options is open. The outcome goes back to the main
/// thread through the game's dispatcher, because only the main thread may show UI.
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

    /// <summary>Whether a city is loaded for playing, so it can be exported. Main thread only.</summary>
    public static bool IsCityLoaded => _cityLoaded;

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
            if (!_cityLoaded)
            {
                ShowMessage("Load a city first. The exporter works in a running city, not in the main menu or the editors.", isError: false);
                return;
            }

            if (IsExportRunning())
            {
                ShowMessage("An export is already running. Wait for its message.", isError: false);
                return;
            }

            StartExport(outcome => ShowMessage(outcome.Message, !outcome.Succeeded));
        }
        catch (Exception e)
        {
            // Never let an exception reach the game's UI code.
            ForgetActiveJob();
            GameLog.Warning("The export couldn't start: " + e);
            ShowMessage("The export couldn't start: " + e.Message + "\n\nDetails are in the game log.", isError: true);
        }
    }

    /// <summary>
    /// Starts an export without UI, for autorun. <paramref name="onFinished"/> receives the outcome on the
    /// main thread, possibly before this method returns if the export can't start.
    /// </summary>
    /// <param name="onFinished">Receives the outcome.</param>
    public static void StartAutomatedExport(Action<ExportOutcome> onFinished)
    {
        ExportOutcome? refusal = null;
        if (!_cityLoaded)
        {
            refusal = new ExportOutcome(false, "No city is loaded, so there is nothing to export.", null);
        }
        else if (IsExportRunning())
        {
            refusal = new ExportOutcome(false, "Another export is already running.", null);
        }
        else
        {
            try
            {
                StartExport(onFinished);
            }
            catch (Exception e)
            {
                ForgetActiveJob();
                GameLog.Warning("The export couldn't start: " + e);
                refusal = new ExportOutcome(false, "The export couldn't start: " + e.Message, null);
            }
        }

        if (refusal is not null)
        {
            onFinished(refusal);
        }
    }

    private static bool IsExportRunning()
    {
        lock (_lock)
        {
            return _activeJob is not null;
        }
    }

    private static void ForgetActiveJob()
    {
        lock (_lock)
        {
            _activeJob = null;
        }
    }

    private static void StartExport(Action<ExportOutcome> onFinished)
    {
        ExportJob job = ExportJob.Prepare(DumpsFolder);
        lock (_lock)
        {
            _activeJob = job;
        }

        GameLog.Info("Export requested.");
        SimulationManager.instance.AddAction(() => RunOnSimulationThread(job, onFinished));
    }

    // Nothing may escape: the simulation thread must survive, and the caller must always get an outcome.
    private static void RunOnSimulationThread(ExportJob job, Action<ExportOutcome> onFinished)
    {
        ExportOutcome outcome;
        try
        {
            outcome = job.Run();
        }
        catch (Exception e)
        {
            // Run doesn't throw, so this would be a bug.
            GameLog.Warning("The export failed unexpectedly: " + e);
            outcome = new ExportOutcome(false, "The export failed unexpectedly: " + e.Message, null);
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

        try
        {
            ThreadHelper.dispatcher.Dispatch(() => Deliver(onFinished, outcome));
        }
        catch (Exception e)
        {
            GameLog.Warning("The export's outcome couldn't be passed to the main thread: " + e);
        }
    }

    private static void Deliver(Action<ExportOutcome> onFinished, ExportOutcome outcome)
    {
        try
        {
            onFinished(outcome);
        }
        catch (Exception e)
        {
            // The dispatcher runs this from the game's main loop, which must not see it.
            GameLog.Warning("The export's outcome couldn't be delivered: " + e);
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
