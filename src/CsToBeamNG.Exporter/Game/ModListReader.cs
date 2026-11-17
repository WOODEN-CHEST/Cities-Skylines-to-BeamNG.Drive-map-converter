using System;
using System.Collections.Generic;
using System.Globalization;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CsToBeamNG.Dump.Manifest;
using ICities;

namespace CsToBeamNG.Exporter.Game;

/// <summary>Lists the enabled mods (docs/research/cs1-save-and-world.md, Runtime and modding facts).</summary>
internal static class ModListReader
{
    /// <summary>Returns the enabled mods, built-in ones included. Main thread only.</summary>
    /// <param name="problems">Receives a message for each mod that couldn't be read properly.</param>
    public static IList<GameModInfo> ReadEnabledMods(ICollection<string> problems)
    {
        var mods = new List<GameModInfo>();
        foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
        {
            try
            {
                if (plugin.isEnabled)
                {
                    mods.Add(new GameModInfo(GetName(plugin, problems), GetWorkshopId(plugin.publishedFileID)));
                }
            }
            catch (Exception e)
            {
                // One misbehaving mod mustn't stop the export, nor hide the others.
                problems.Add("A mod couldn't be listed: " + e.GetType().Name + ": " + e.Message);
            }
        }

        return mods;
    }

    // The name that Content Manager shows comes from the mod's own IUserMod.Name, which runs the mod's
    // code; the plugin's folder name stands in when that fails.
    private static string GetName(PluginManager.PluginInfo plugin, ICollection<string> problems)
    {
        try
        {
            if (plugin.userModInstance is IUserMod userMod && !string.IsNullOrEmpty(userMod.Name))
            {
                return userMod.Name;
            }
        }
        catch (Exception e)
        {
            problems.Add("The mod in '" + plugin.name + "' didn't report its name: " + e.GetType().Name + ": " + e.Message);
        }

        return plugin.name;
    }

    // Local and built-in mods have the invalid id; workshop mods have their item's id.
    private static string? GetWorkshopId(PublishedFileId id) =>
        id == PublishedFileId.invalid ? null : id.AsUInt64.ToString(CultureInfo.InvariantCulture);
}
