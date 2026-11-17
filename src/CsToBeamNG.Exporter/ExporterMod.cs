using System;
using CsToBeamNG.Exporter.Export;
using CsToBeamNG.Exporter.Game;
using ICities;

namespace CsToBeamNG.Exporter;

/// <summary>
/// The mod's entry point. Cities: Skylines lists every <see cref="IUserMod"/> it finds in the mods
/// folder in Content Manager → Mods.
/// </summary>
public sealed class ExporterMod : IUserMod
{
    /// <inheritdoc/>
    public string Name => "CS to BeamNG Exporter";

    /// <inheritdoc/>
    public string Description => "Exports the loaded city for conversion into a BeamNG.drive level.";

    /// <summary>
    /// Builds the mod's page in Options. The game finds this method by reflection; it isn't part of
    /// <see cref="IUserMod"/> (docs/research/cs1-save-and-world.md, Runtime and modding facts).
    /// </summary>
    /// <param name="helper">Adds controls to the page.</param>
#pragma warning disable CA1822 // The game calls it on the mod's instance, so it can't be static.
    public void OnSettingsUI(UIHelperBase helper)
#pragma warning restore CA1822
    {
        UIHelperBase group = helper.AddGroup(MessagePanel.Title);
        group.AddButton("Export city", ExportController.OnExportClicked);
        try
        {
            string text = "Dumps are saved in " + ExportController.DumpsFolder;
            if (!SettingsUi.AddLabel(group, text))
            {
                GameLog.Warning("The dumps folder couldn't be shown in Options. " + text);
            }
        }
        catch (Exception e)
        {
            // The label is only information; the page must still work without it.
            GameLog.Warning("The dumps folder couldn't be shown in Options: " + e);
        }
    }
}
