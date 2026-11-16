using System;
using System.Collections.Generic;
using System.IO;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>A game installed through Steam.</summary>
/// <param name="InstallDir">The game's folder, <c>steamapps/common/&lt;installdir&gt;</c>.</param>
/// <param name="LibraryPath">The Steam library that contains it.</param>
/// <param name="BuildId">The Steam build id from the app manifest, if there is one.</param>
internal sealed record SteamApp(string InstallDir, string LibraryPath, string? BuildId);

/// <summary>
/// Finds Steam folders and libraries, and games in them through their app manifests. The layout
/// is described in docs/research/game-locations.md (Steam).
/// </summary>
internal static class SteamLocator
{
    private const string SteamAppsDirName = "steamapps";

    // Older Steam versions capitalised the folder; that matters on case-sensitive file systems.
    private const string LegacySteamAppsDirName = "SteamApps";
    private const string CommonDirName = "common";
    private const string ConfigDirName = "config";
    private const string LibraryFoldersFileName = "libraryfolders.vdf";

    public static SteamInfo Locate(DiscoveryContext context, PathTools paths, List<DiscoveryProblem> problems)
    {
        List<FoundPath> roots = FindRoots(context, paths, problems);
        List<string> libraries = FindLibraries(roots, paths, problems);
        return new SteamInfo(roots, libraries);
    }

    /// <summary>Returns the <c>steamapps</c> folder of a Steam folder or library, or <see langword="null"/>.</summary>
    public static string? FindSteamAppsDir(string libraryPath)
    {
        string current = Path.Join(libraryPath, SteamAppsDirName);
        if (Directory.Exists(current))
        {
            return current;
        }

        string legacy = Path.Join(libraryPath, LegacySteamAppsDirName);
        return Directory.Exists(legacy) ? legacy : null;
    }

    /// <summary>
    /// Finds an installed game through its <c>appmanifest_&lt;appId&gt;.acf</c> in any library.
    /// Without a manifest, a folder with the game's default name still counts, with a warning.
    /// </summary>
    public static SteamApp? FindApp(
        SteamInfo steam,
        string appId,
        string defaultInstallDirName,
        string gameName,
        List<DiscoveryProblem> problems)
    {
        var found = new List<SteamApp>();
        foreach (string library in steam.Libraries)
        {
            string? steamApps = FindSteamAppsDir(library);
            string? manifestPath = steamApps is null ? null : Path.Join(steamApps, "appmanifest_" + appId + ".acf");
            if (steamApps is null || manifestPath is null || !File.Exists(manifestPath))
            {
                continue;
            }

            KeyValuesNode? document = ReadKeyValues(manifestPath, gameName, problems);
            if (document is null)
            {
                continue;
            }

            KeyValuesNode? appState = document.Find("AppState");
            if (appState is null)
            {
                problems.AddWarning($"{gameName}: the Steam app manifest '{manifestPath}' has no AppState section.");
                continue;
            }

            string installDirName = appState.FindValue("installdir") is { Length: > 0 } name ? name : defaultInstallDirName;
            found.Add(new SteamApp(Path.Join(steamApps, CommonDirName, installDirName), library, appState.FindValue("buildid")));
        }

        if (found.Count > 1)
        {
            problems.AddWarning($"{gameName}: several Steam libraries have an app manifest for it; using '{found[0].InstallDir}'.");
        }

        return found.Count > 0 ? found[0] : FindAppWithoutManifest(steam, defaultInstallDirName, gameName, problems);
    }

    /// <summary>Reads a KeyValues file. An unreadable or malformed file is reported as a warning.</summary>
    internal static KeyValuesNode? ReadKeyValues(string path, string subject, List<DiscoveryProblem> problems)
    {
        try
        {
            return KeyValuesParser.Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            problems.AddWarning($"{subject}: could not read '{path}': {e.Message}");
            return null;
        }
    }

    private static SteamApp? FindAppWithoutManifest(
        SteamInfo steam,
        string defaultInstallDirName,
        string gameName,
        List<DiscoveryProblem> problems)
    {
        // For example a library copied by hand: the game is there, but Steam doesn't know about it.
        foreach (string library in steam.Libraries)
        {
            string? steamApps = FindSteamAppsDir(library);
            string? candidate = steamApps is null ? null : Path.Join(steamApps, CommonDirName, defaultInstallDirName);
            if (candidate is not null && Directory.Exists(candidate))
            {
                problems.AddWarning($"{gameName}: found at '{candidate}', but Steam has no app manifest for it.");
                return new SteamApp(candidate, library, BuildId: null);
            }
        }

        return null;
    }

    private static List<FoundPath> FindRoots(DiscoveryContext context, PathTools paths, List<DiscoveryProblem> problems)
    {
        var roots = new List<FoundPath>();
        if (context.Overrides.SteamDir is { } steamOverride)
        {
            // An explicit Steam folder replaces the search, so that a wrong guess can't sneak in.
            string path = paths.Normalize(steamOverride.Path);
            bool isSteam = FindSteamAppsDir(path) is not null;
            if (!isSteam)
            {
                problems.AddError($"Steam: {steamOverride.Origin} is set to '{steamOverride.Path}', which is not a Steam folder (it has no steamapps folder).");
            }

            roots.Add(new FoundPath(path, LocationSource.Override, isSteam, steamOverride.Origin));
            return roots;
        }

        foreach (Candidate candidate in RootCandidates(context, paths))
        {
            if (FindSteamAppsDir(candidate.Path) is null)
            {
                continue;
            }

            string canonical = paths.Canonicalize(candidate.Path);
            if (!roots.Exists(root => paths.AreSame(root.Path, canonical)))
            {
                roots.Add(new FoundPath(canonical, candidate.Source, Exists: true, candidate.Detail));
            }
        }

        if (roots.Count == 0)
        {
            problems.AddWarning("Steam: no Steam installation found. Games installed without Steam must be given as overrides.");
        }

        return roots;
    }

    private static IEnumerable<Candidate> RootCandidates(DiscoveryContext context, PathTools paths)
    {
        if (context.Os == OsKind.Windows)
        {
            string? userPath = context.Registry?.GetString(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            if (!string.IsNullOrWhiteSpace(userPath))
            {
                yield return new Candidate(userPath, LocationSource.Registry, @"HKCU\Software\Valve\Steam\SteamPath");
            }

            string? machinePath = context.Registry?.GetString(RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!string.IsNullOrWhiteSpace(machinePath))
            {
                yield return new Candidate(machinePath, LocationSource.Registry, @"HKLM\SOFTWARE\WOW6432Node\Valve\Steam\InstallPath");
            }

            if (!string.IsNullOrEmpty(context.ProgramFilesX86))
            {
                yield return new Candidate(Path.Join(context.ProgramFilesX86, "Steam"), LocationSource.Default);
            }

            yield break;
        }

        // The details say which candidate led to a root, since links usually make several lead to one.
        string home = context.HomeDir;
        string dataHomeName = string.IsNullOrWhiteSpace(context.XdgDataHome) ? "~/.local/share" : "$XDG_DATA_HOME";
        yield return new Candidate(Path.Join(home, ".steam", "steam"), LocationSource.Default, "via ~/.steam/steam");
        yield return new Candidate(Path.Join(home, ".steam", "root"), LocationSource.Default, "via ~/.steam/root");
        yield return new Candidate(Path.Join(paths.DataHome, "Steam"), LocationSource.Default, $"via {dataHomeName}/Steam");
        yield return new Candidate(Path.Join(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"), LocationSource.Default, "Flatpak Steam");
        yield return new Candidate(Path.Join(home, "snap", "steam", "common", ".local", "share", "Steam"), LocationSource.Default, "Snap Steam");
    }

    private static List<string> FindLibraries(List<FoundPath> roots, PathTools paths, List<DiscoveryProblem> problems)
    {
        var libraries = new List<string>();
        foreach (FoundPath root in roots)
        {
            if (!root.Exists)
            {
                continue;
            }

            // Every Steam folder is a library itself, whether or not the list names it.
            AddLibrary(root.Path);
            string? listFile = FindLibraryFoldersFile(root.Path);
            KeyValuesNode? document = listFile is null ? null : ReadKeyValues(listFile, "Steam", problems);
            if (listFile is null || document is null)
            {
                continue;
            }

            foreach (string listed in ReadLibraryPaths(document))
            {
                string path = paths.Normalize(listed);
                if (Directory.Exists(path))
                {
                    AddLibrary(paths.Canonicalize(path));
                }
                else
                {
                    problems.AddWarning($"Steam: the library '{listed}', listed in '{listFile}', does not exist. Is its drive connected?");
                }
            }
        }

        return libraries;

        void AddLibrary(string path)
        {
            if (!libraries.Exists(library => paths.AreSame(library, path)))
            {
                libraries.Add(path);
            }
        }
    }

    private static string? FindLibraryFoldersFile(string rootPath)
    {
        // Current Steam keeps the list in steamapps; older versions kept it in config.
        string? steamApps = FindSteamAppsDir(rootPath);
        string[] candidates =
        [
            steamApps is null ? string.Empty : Path.Join(steamApps, LibraryFoldersFileName),
            Path.Join(rootPath, ConfigDirName, LibraryFoldersFileName),
        ];
        return Array.Find(candidates, candidate => candidate.Length > 0 && File.Exists(candidate));
    }

    /// <summary>
    /// Reads the library folders listed in <c>libraryfolders.vdf</c>. Current Steam writes
    /// <c>"&lt;n&gt;" { "path" "&lt;folder&gt;" ... }</c>; older versions wrote <c>"&lt;n&gt;" "&lt;folder&gt;"</c>
    /// next to other, non-numeric keys.
    /// </summary>
    internal static List<string> ReadLibraryPaths(KeyValuesNode document)
    {
        var paths = new List<string>();
        KeyValuesNode? list = document.Find("libraryfolders");
        if (list is null)
        {
            return paths;
        }

        foreach (KeyValuesNode entry in list.Children)
        {
            if (!IsNumber(entry.Key))
            {
                continue;
            }

            string? path = entry.Value ?? entry.FindValue("path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    internal static bool IsNumber(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return text.Length > 0;
    }
}
