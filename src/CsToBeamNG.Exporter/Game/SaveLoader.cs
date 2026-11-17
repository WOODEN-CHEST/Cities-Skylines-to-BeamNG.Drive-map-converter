using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Packaging;
using ColossalFramework.PlatformServices;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Loads a save by name, the way the main menu's Continue does (<c>MainMenu.ContinueGame</c>), for
/// autorun. Main thread only (docs/research/cs1-save-and-world.md, Loading a save from a mod).
/// </summary>
internal static class SaveLoader
{
    // The scenes the game loads for playing a city, as in MainMenu.ContinueGame.
    private const string PlayerScene = "Game";
    private const string UiScene = "InGame";

    /// <summary>Starts loading the save. Returns <see langword="null"/> if loading started, otherwise why not.</summary>
    /// <param name="saveName">The save's name, as the Load panel lists it, or its file name.</param>
    public static string? StartLoading(string saveName)
    {
        var assets = new List<Package.Asset>();
        var metaData = new List<SaveGameMetaData>();
        var candidates = new List<SaveCandidate>();
        foreach (Package.Asset asset in PackageManager.FilterAssets(UserAssetType.SaveGameMetaData))
        {
            // The same filter as the Load panel's list.
            if (asset == null || !asset.isEnabled || PackageHelper.IsDemoModeSave(asset))
            {
                continue;
            }

            SaveGameMetaData? save;
            try
            {
                save = asset.Instantiate<SaveGameMetaData>();
            }
            catch (Exception e)
            {
                GameLog.Warning("Autorun: the save \"" + asset.name + "\" can't be read, so it is skipped: " + e.Message);
                continue;
            }

            if (save is null)
            {
                continue;
            }

            assets.Add(asset);
            metaData.Add(save);
            candidates.Add(new SaveCandidate(asset.name, SaveCandidate.FileNameFromPackagePath(asset.package?.packagePath), save.timeStamp));
        }

        int index = SaveMatcher.FindBest(candidates, saveName);
        if (index < 0)
        {
            return SaveMatcher.DescribeNotFound(candidates, saveName);
        }

        Package.Asset chosen = assets[index];
        SaveGameMetaData chosenSave = metaData[index];
        GameLog.Info("Autorun: loading \"" + chosen.name + "\" (city \"" + chosenSave.cityName + "\") from " + (chosen.package?.packagePath ?? "an unknown package") + ".");
        Coroutine? loading = Singleton<LoadingManager>.instance.LoadLevel(chosenSave.assetRef, PlayerScene, UiScene, CreateSimulationMetaData(chosen, chosenSave), false);
        return loading is null ? "The game refused to load the save, because it is already loading something or quitting." : null;
    }

    // The same settings as MainMenu.ContinueGame and LoadPanel.LoadRoutine, without the Load panel's
    // choices (map theme override, forced environment), which keep the save's own.
    private static SimulationMetaData CreateSimulationMetaData(Package.Asset asset, SaveGameMetaData save)
    {
        var simulation = new SimulationMetaData
        {
            m_CityName = save.cityName,
            m_updateMode = SimulationManager.UpdateMode.LoadGame,
        };
        if (asset.package != null && asset.package.GetPublishedFileID() != PublishedFileId.invalid)
        {
            simulation.m_disableAchievements = SimulationMetaData.MetaBool.True;
        }

        if (!string.IsNullOrEmpty(save.mapThemeRef))
        {
            Package.Asset theme = PackageManager.FindAssetByName(save.mapThemeRef, UserAssetType.MapThemeMetaData);
            if (theme != null)
            {
                simulation.m_MapThemeMetaData = theme.Instantiate<MapThemeMetaData>();
                simulation.m_MapThemeMetaData.SetSelfRef(theme);
            }
        }

        return simulation;
    }
}
