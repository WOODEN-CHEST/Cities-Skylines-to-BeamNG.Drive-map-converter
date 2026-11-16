using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Finds Cities: Skylines: its install, user data, savegames, workshop items and log. The
/// locations are described in docs/research/game-locations.md (Cities: Skylines).
/// </summary>
internal static class CitiesSkylinesLocator
{
    internal const string AppId = "255710";
    private const string GameName = "Cities: Skylines";
    private const string DefaultInstallDirName = "Cities_Skylines";
    private const string DataDirName = "Cities_Data";
    private const string ManagedDirName = "Managed";
    private const string MarkerAssemblyFileName = "Assembly-CSharp.dll";
    private const string CompanyDirName = "Colossal Order";
    private const string UserDirName = "Cities_Skylines";
    private const string SaveFilePattern = "*.crp";

    // Unity names the log folder after the product, with the ':' of "Cities: Skylines" replaced by '_'.
    private const string LogProductDirName = "Cities_ Skylines";
    private const string LinuxLogFileName = "Player.log";
    private const string WindowsLogFileName = "output_log.txt";

    // Our own names: the exporter's mod folder (step 0.1) and its dump folder (step 0.4).
    private const string ExporterModDirName = "CsToBeamNG";
    private const string ExporterAssemblyFileName = "CsToBeamNG.Exporter.dll";
    private const string DumpsParentDirName = "CsToBeamNG";
    private const string DumpsDirName = "dumps";

    private static readonly EnumerationOptions _fileEnumeration = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        IgnoreInaccessible = true,
    };

    public static CitiesSkylinesInfo Locate(
        DiscoveryContext context,
        PathTools paths,
        SteamInfo steam,
        List<DiscoveryProblem> problems)
    {
        (FoundPath? install, SteamApp? app) = LocateInstall(context, paths, steam, problems);
        FoundPath? managed = null;
        if (install is { Exists: true })
        {
            managed = DerivedDir(Path.Join(install.Path, DataDirName, ManagedDirName));
            if (!File.Exists(Path.Join(managed.Path, MarkerAssemblyFileName)))
            {
                problems.AddError($"{GameName}: '{install.Path}' does not look like the game's install folder: {DataDirName}/{ManagedDirName}/{MarkerAssemblyFileName} is missing.");
            }
        }

        FoundPath? user = LocateUserDir(context, paths, steam, problems);
        FoundPath? mods = null;
        FoundPath? exporterMod = null;
        FoundPath? dumpRoot = null;
        string? exporterVersion = null;
        if (user is { Exists: true })
        {
            mods = DerivedDir(Path.Join(user.Path, "Addons", "Mods"));
            if (!mods.Exists)
            {
                problems.AddWarning($"{GameName}: the mods folder '{mods.Path}' does not exist. Start the game once to create it.");
            }

            exporterMod = DerivedDir(Path.Join(mods.Path, ExporterModDirName));
            exporterVersion = ReadExporterVersion(exporterMod.Path, problems);
            dumpRoot = DerivedDir(Path.Join(user.Path, DumpsParentDirName, DumpsDirName));
        }

        (FoundPath? workshop, int workshopItems) = LocateWorkshop(steam, app);
        return new CitiesSkylinesInfo
        {
            InstallDir = install,
            SteamBuildId = app?.BuildId,
            ManagedDir = managed,
            UserDir = user,
            ModsDir = mods,
            ExporterModDir = exporterMod,
            ExporterModVersion = exporterVersion,
            DumpRoot = dumpRoot,
            WorkshopDir = workshop,
            WorkshopItemCount = workshopItems,
            LogFile = LocateLog(context, paths, install, user),
            Saves = FindSaves(steam, user, problems),
        };
    }

    private static (FoundPath? Install, SteamApp? App) LocateInstall(
        DiscoveryContext context,
        PathTools paths,
        SteamInfo steam,
        List<DiscoveryProblem> problems)
    {
        if (context.Overrides.CitiesSkylinesDir is { } installOverride)
        {
            return (paths.FromOverride(installOverride, $"{GameName} install folder", problems), null);
        }

        SteamApp? app = SteamLocator.FindApp(steam, AppId, DefaultInstallDirName, GameName, problems);
        if (app is null)
        {
            problems.AddError($"{GameName}: the install folder was not found in any Steam library. Install the game with Steam, or set {DiscoveryOverrides.CitiesSkylinesDirVariable}.");
            return (null, null);
        }

        bool exists = Directory.Exists(app.InstallDir);
        if (!exists)
        {
            problems.AddError($"{GameName}: Steam lists the game in '{app.InstallDir}', but that folder does not exist.");
        }

        return (new FoundPath(app.InstallDir, LocationSource.SteamLibrary, exists, app.LibraryPath), app);
    }

    private static FoundPath? LocateUserDir(
        DiscoveryContext context,
        PathTools paths,
        SteamInfo steam,
        List<DiscoveryProblem> problems)
    {
        const string Subject = GameName + " user data folder";
        if (context.Overrides.CitiesSkylinesUserDir is { } userOverride)
        {
            return paths.FromOverride(userOverride, Subject, problems);
        }

        var candidates = new List<Candidate>();
        if (context.Os == OsKind.Windows)
        {
            if (!string.IsNullOrEmpty(context.LocalAppData))
            {
                candidates.Add(new Candidate(Path.Join(context.LocalAppData, CompanyDirName, UserDirName), LocationSource.Default));
            }
        }
        else
        {
            candidates.Add(new Candidate(Path.Join(paths.DataHome, CompanyDirName, UserDirName), LocationSource.Default));
            candidates.AddRange(paths.FlatpakSteamDataHomes.Select(dataHome =>
                new Candidate(Path.Join(dataHome, CompanyDirName, UserDirName), LocationSource.Default, "Flatpak Steam")));
            candidates.AddRange(ProtonPrefixes.LocalAppDataDirs(steam, AppId).Select(localAppData =>
                new Candidate(Path.Join(localAppData, CompanyDirName, UserDirName), LocationSource.ProtonPrefix, "Proton prefix")));
        }

        FoundPath? user = Candidate.ChooseFirstExisting(candidates, Subject, paths, problems);
        if (user is null || !user.Exists)
        {
            string expected = user is null ? string.Empty : $"; expected '{user.Path}'";
            problems.AddError($"{Subject}: not found{expected}. Start the game once, or set {DiscoveryOverrides.CitiesSkylinesUserDirVariable}.");
        }

        return user;
    }

    private static string? ReadExporterVersion(string modDir, List<DiscoveryProblem> problems)
    {
        string assemblyPath = Path.Join(modDir, ExporterAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            problems.AddInfo($"{GameName}: the CsToBeamNG exporter mod is not in '{modDir}'. Building the solution where the game is installed deploys it.");
            return null;
        }

        try
        {
            return AssemblyName.GetAssemblyName(assemblyPath).Version?.ToString();
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            problems.AddWarning($"{GameName}: the exporter mod '{assemblyPath}' can't be read: {e.Message}");
            return null;
        }
    }

    private static (FoundPath? Dir, int ItemCount) LocateWorkshop(SteamInfo steam, SteamApp? app)
    {
        // Workshop items live in the library that holds the game; check that one first.
        IEnumerable<string> libraries = app is null ? steam.Libraries : steam.Libraries.Prepend(app.LibraryPath);
        foreach (string library in libraries)
        {
            string? steamApps = SteamLocator.FindSteamAppsDir(library);
            string? workshop = steamApps is null ? null : Path.Join(steamApps, "workshop", "content", AppId);
            if (workshop is not null && Directory.Exists(workshop))
            {
                return (DerivedDir(workshop), Directory.EnumerateDirectories(workshop).Count());
            }
        }

        return (null, 0);
    }

    private static FoundPath? LocateLog(DiscoveryContext context, PathTools paths, FoundPath? install, FoundPath? user)
    {
        // The Windows build, natively or through Proton, writes Unity's log next to its data
        // (inferred); the native Linux build writes it to Unity's config folder (verified).
        bool windowsBuild = context.Os == OsKind.Windows || user?.Source == LocationSource.ProtonPrefix;
        if (windowsBuild)
        {
            return install is null ? null : DerivedFile(Path.Join(install.Path, DataDirName, WindowsLogFileName));
        }

        return DerivedFile(Path.Join(paths.ConfigHome, "unity3d", CompanyDirName, LogProductDirName, LinuxLogFileName));
    }

    private static List<SaveGame> FindSaves(SteamInfo steam, FoundPath? user, List<DiscoveryProblem> problems)
    {
        var saves = new List<SaveGame>();
        if (user is { Exists: true })
        {
            saves.AddRange(ReadSaves(Path.Join(user.Path, "Saves"), SaveLocation.Local, steamAccountId: null, problems));
        }

        // Steam Cloud keeps its local copy per Steam account: userdata/<account id>/<app id>/remote.
        foreach (FoundPath root in steam.Roots)
        {
            string userData = Path.Join(root.Path, "userdata");
            if (!root.Exists || !Directory.Exists(userData))
            {
                continue;
            }

            foreach (string accountDir in ListDirectories(userData, problems))
            {
                string accountId = Path.GetFileName(accountDir);
                if (SteamLocator.IsNumber(accountId))
                {
                    saves.AddRange(ReadSaves(Path.Join(accountDir, AppId, "remote"), SaveLocation.SteamCloud, accountId, problems));
                }
            }
        }

        return saves;
    }

    private static List<SaveGame> ReadSaves(string dir, SaveLocation location, string? steamAccountId, List<DiscoveryProblem> problems)
    {
        var saves = new List<SaveGame>();
        if (!Directory.Exists(dir))
        {
            return saves;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, SaveFilePattern, _fileEnumeration))
            {
                var info = new FileInfo(file);
                saves.Add(new SaveGame(Path.GetFileNameWithoutExtension(file), file, info.Length, info.LastWriteTimeUtc, location, steamAccountId));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.AddWarning($"{GameName}: could not list the savegames in '{dir}': {e.Message}");
        }

        // Case-insensitive first, so that "CIty B" sorts after "City A"; ordinal breaks ties.
        saves.Sort((first, second) =>
        {
            int byName = string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.Compare(first.Name, second.Name, StringComparison.Ordinal);
        });
        return saves;
    }

    private static List<string> ListDirectories(string dir, List<DiscoveryProblem> problems)
    {
        try
        {
            return Directory.EnumerateDirectories(dir).Order(StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.AddWarning($"{GameName}: could not list '{dir}': {e.Message}");
            return [];
        }
    }

    private static FoundPath DerivedDir(string path) => new(path, LocationSource.Derived, Directory.Exists(path));

    private static FoundPath DerivedFile(string path) => new(path, LocationSource.Derived, File.Exists(path));
}
