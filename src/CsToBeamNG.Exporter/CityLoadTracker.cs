using System;
using CsToBeamNG.Exporter.Export;
using CsToBeamNG.Exporter.Game;
using ICities;

namespace CsToBeamNG.Exporter;

/// <summary>
/// Tells <see cref="ExportController"/> when a city is loaded and unloaded. The game creates an instance
/// of every <see cref="ILoadingExtension"/> in an enabled mod when a level loads.
/// </summary>
public sealed class CityLoadTracker : LoadingExtensionBase
{
    /// <inheritdoc/>
    public override void OnLevelLoaded(LoadMode mode)
    {
        try
        {
            // currentMode is Game for every way of playing a city (new, loaded, from a scenario) and an
            // editor mode otherwise, which is simpler than listing LoadMode values.
            AppMode appMode = loadingManager?.currentMode ?? AppMode.Game;
            ExportController.OnLevelLoaded(mode, appMode);
        }
        catch (Exception e)
        {
            // The game would report an exception from here as an error of this mod.
            GameLog.Warning("Couldn't track the loaded level: " + e);
        }
    }

    /// <inheritdoc/>
    public override void OnLevelUnloading()
    {
        try
        {
            ExportController.OnLevelUnloading();
        }
        catch (Exception e)
        {
            GameLog.Warning("Couldn't track the level unloading: " + e);
        }
    }
}
