using System.Collections.Generic;
using System.IO;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// The Proton (Wine) prefixes that Steam keeps for games run through Proton on Linux. Inside a
/// prefix, a game sees the Windows layout. See docs/research/game-locations.md (Proton).
/// </summary>
internal static class ProtonPrefixes
{
    /// <summary>
    /// The Windows <c>%LOCALAPPDATA%</c> folder inside the app's prefix in each Steam library, whether
    /// it exists or not.
    /// </summary>
    public static IEnumerable<string> LocalAppDataDirs(SteamInfo steam, string appId)
    {
        foreach (string library in steam.Libraries)
        {
            string? steamApps = SteamLocator.FindSteamAppsDir(library);
            if (steamApps is not null)
            {
                yield return Path.Join(steamApps, "compatdata", appId, "pfx", "drive_c", "users", "steamuser", "AppData", "Local");
            }
        }
    }
}
