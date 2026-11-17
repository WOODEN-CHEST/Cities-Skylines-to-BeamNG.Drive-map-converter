using System;
using System.Collections.Generic;
using ColossalFramework;
using CsToBeamNG.Dump.Autorun;
using CsToBeamNG.Exporter.Core;
using CsToBeamNG.Exporter.Export;
using CsToBeamNG.Exporter.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CsToBeamNG.Exporter.Autorun;

/// <summary>
/// Runs an <see cref="AutorunSequence"/> in the game: a component on its own game object that survives
/// scene changes, updated every frame on the main thread, and the game side of <see cref="IAutorunHost"/>.
/// </summary>
internal sealed class AutorunDriver : MonoBehaviour, IAutorunHost
{
    // The main menu's scene, which the game loads additively when the menu is ready
    // (docs/research/cs1-save-and-world.md, Loading a save from a mod).
    private const string MenuSceneName = "MainMenu";

    private AutorunSequence? _sequence;
    private StatusReporter? _reporter;
    private IList<string> _probes = new string[0];

    /// <inheritdoc/>
    public bool IsMenuIdle
    {
        get
        {
            if (!Singleton<LoadingManager>.exists)
            {
                return false;
            }

            LoadingManager loading = Singleton<LoadingManager>.instance;
            return !loading.m_currentlyLoading
                && !loading.m_applicationQuitting
                && SceneManager.GetSceneByName(MenuSceneName).isLoaded;
        }
    }

    /// <inheritdoc/>
    public bool IsCityLoaded => ExportController.IsCityLoaded;

    /// <summary>Starts carrying out a request. Main thread only.</summary>
    /// <param name="request">The request.</param>
    /// <param name="reporter">Writes the status.</param>
    public static void Begin(AutorunRequest request, StatusReporter reporter)
    {
        var host = new GameObject("CsToBeamNG Autorun");
        DontDestroyOnLoad(host);
        AutorunDriver driver = host.AddComponent<AutorunDriver>();
        driver._reporter = reporter;
        driver._probes = request.Probes;
        driver._sequence = new AutorunSequence(request, driver);
        driver._sequence.Start(Time.realtimeSinceStartup);
    }

    /// <inheritdoc/>
    public string? StartLoadingSave(string saveName) => SaveLoader.StartLoading(saveName);

    /// <inheritdoc/>
    public void StartExport() => ExportController.StartAutomatedExport(_probes, outcome =>
        _sequence?.OnExportFinished(outcome.Succeeded, outcome.Message, outcome.DumpFolder));

    /// <inheritdoc/>
    public void ReportStatus(AutorunStatus status) => _reporter?.Report(status);

    /// <inheritdoc/>
    public void Quit()
    {
        // The Exit button's confirmation does the same; it returns null when the game already quits.
        _ = Singleton<LoadingManager>.instance.QuitApplication();
    }

    /// <inheritdoc/>
    public void ForceQuit() => Application.Quit();

    /// <inheritdoc/>
    public void Log(string message) => GameLog.Info(message);

    /// <inheritdoc/>
    public void Warn(string message) => GameLog.Warning(message);

    // Called by Unity every frame. realtimeSinceStartup keeps running while the game is paused.
    private void Update()
    {
        if (_sequence is null)
        {
            return;
        }

        try
        {
            _sequence.Update(Time.realtimeSinceStartup);
        }
        catch (Exception e)
        {
            GameLog.Warning("Autorun: unexpected error: " + e);
            try
            {
                _sequence.Fail("The exporter's autorun failed: " + e.Message);
            }
            catch (Exception inner)
            {
                GameLog.Warning("Autorun: the failure couldn't be handled: " + inner);
            }
        }
    }
}
