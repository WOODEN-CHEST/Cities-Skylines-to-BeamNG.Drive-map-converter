using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Finds BeamNG.drive: its install, version and user folder. The locations and files are described
/// in docs/research/game-locations.md (BeamNG.drive).
/// </summary>
internal static partial class BeamNGLocator
{
    internal const string AppId = "284160";
    private const string GameName = "BeamNG.drive";
    private const string DefaultInstallDirName = "BeamNG.drive";

    // Since 0.37 the user folder is <data>/BeamNG/BeamNG.drive/current, with BeamNG.drive.ini (the
    // version that last ran) next to the BeamNG.drive folder. Before 0.37 it was
    // <data>/BeamNG.drive/<major.minor>.
    private const string CompanyDirName = "BeamNG";
    private const string ProductDirName = "BeamNG.drive";
    private const string CurrentDirName = "current";
    private const string VersionIniFileName = "BeamNG.drive.ini";

    // In the install folder.
    private const string StartupIniFileName = "startup.ini";
    private const string UserPathSetting = "UserPath";
    private const string IntegrityFileName = "integrity.json";

    // In the user folder.
    private const string LogFileName = "beamng.log";

    private static readonly Version _minimumVersion = new(0, 40);

    public static BeamNGInfo Locate(
        DiscoveryContext context,
        PathTools paths,
        SteamInfo steam,
        List<DiscoveryProblem> problems)
    {
        (FoundPath? install, SteamApp? app) = LocateInstall(context, paths, steam, problems);
        VersionInfo? installed = install is { Exists: true } ? ReadIntegrityVersion(install.Path, problems) : null;

        FoundPath? user = LocateUserDir(context, paths, steam, install, problems);
        VersionInfo? lastRun = null;
        if (user is { Exists: true })
        {
            lastRun = ReadIniVersion(user.Path, problems) ?? ReadLogVersion(user.Path, problems);
        }

        ReportOldUserFolders(context, paths, steam, problems);
        VersionInfo? version = installed ?? lastRun;
        CheckVersion(installed, lastRun, version, problems);

        return new BeamNGInfo
        {
            InstallDir = install,
            SteamBuildId = app?.BuildId,
            Version = version?.Version,
            EngineBuild = version?.Build,
            VersionSource = version?.Source,
            UserDir = user,
            UnpackedModsDir = user is { Exists: true } ? Derived(Path.Join(user.Path, "mods", "unpacked"), isFile: false) : null,
            LogFile = user is { Exists: true } ? Derived(Path.Join(user.Path, LogFileName), isFile: true) : null,
        };
    }

    private static (FoundPath? Install, SteamApp? App) LocateInstall(
        DiscoveryContext context,
        PathTools paths,
        SteamInfo steam,
        List<DiscoveryProblem> problems)
    {
        if (context.Overrides.BeamNGDir is { } installOverride)
        {
            return (paths.FromOverride(installOverride, $"{GameName} install folder", problems), null);
        }

        SteamApp? app = SteamLocator.FindApp(steam, AppId, DefaultInstallDirName, GameName, problems);
        if (app is null)
        {
            problems.AddError($"{GameName}: the install folder was not found in any Steam library. Install the game with Steam, or set {DiscoveryOverrides.BeamNGDirVariable}.");
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
        FoundPath? install,
        List<DiscoveryProblem> problems)
    {
        const string Subject = GameName + " user folder";
        if (context.Overrides.BeamNGUserDir is { } userOverride)
        {
            return paths.FromOverride(userOverride, Subject, problems);
        }

        var candidates = new List<Candidate>();
        if (install is { Exists: true } && ReadStartupUserPath(install.Path, problems) is { } fromSetting)
        {
            candidates.Add(fromSetting);
        }

        string? nativeDataDir = context.Os == OsKind.Windows ? context.LocalAppData : paths.DataHome;
        if (!string.IsNullOrEmpty(nativeDataDir))
        {
            candidates.Add(new Candidate(CurrentLayout(nativeDataDir), LocationSource.Default));
        }

        if (context.Os == OsKind.Linux)
        {
            candidates.AddRange(paths.FlatpakSteamDataHomes.Select(dataHome =>
                new Candidate(CurrentLayout(dataHome), LocationSource.Default, "Flatpak Steam")));
            candidates.AddRange(ProtonPrefixes.LocalAppDataDirs(steam, AppId).Select(localAppData =>
                new Candidate(CurrentLayout(localAppData), LocationSource.ProtonPrefix, "Proton prefix")));
        }

        FoundPath? user = Candidate.ChooseFirstExisting(candidates, Subject, paths, problems);
        if (user is null || !user.Exists)
        {
            string expected = user is null ? string.Empty : $"; expected '{user.Path}'";
            problems.AddError($"{Subject}: not found{expected}. Start the game once, or set {DiscoveryOverrides.BeamNGUserDirVariable}.");
        }

        return user;
    }

    /// <summary>
    /// The user folder set by <c>UserPath</c> in the install's <c>startup.ini</c> (the launcher's
    /// "Manage User Folder"). Empty means the default. The game appends <c>current</c>, as it does
    /// for the default location (inferred; see the research notes).
    /// </summary>
    private static Candidate? ReadStartupUserPath(string installDir, List<DiscoveryProblem> problems)
    {
        string iniPath = Path.Join(installDir, StartupIniFileName);
        string? text = ReadText(iniPath, problems);
        string? userPath = text is null ? null : IniFile.ReadValue(text, UserPathSetting);
        if (string.IsNullOrWhiteSpace(userPath))
        {
            return null;
        }

        // startup.ini documents relative values as relative to its own folder, the install folder.
        string baseDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userPath, installDir));
        string current = Path.Join(baseDir, CurrentDirName);
        if (!Directory.Exists(current))
        {
            problems.AddWarning($"{GameName}: {StartupIniFileName} sets {UserPathSetting} to '{userPath}', but '{current}' does not exist; using the default locations instead.");
            return null;
        }

        return new Candidate(current, LocationSource.GameSetting, $"{StartupIniFileName} {UserPathSetting}");
    }

    private static string CurrentLayout(string dataDir) => Path.Join(dataDir, CompanyDirName, ProductDirName, CurrentDirName);

    /// <summary>The installed version, from the install's <c>integrity.json</c>.</summary>
    private static VersionInfo? ReadIntegrityVersion(string installDir, List<DiscoveryProblem> problems)
    {
        string path = Path.Join(installDir, IntegrityFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string? version = GetString(root, "version");
            return version is null ? null : new VersionInfo(version, FirstNumber(GetString(root, "buildinfo")), IntegrityFileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            problems.AddWarning($"{GameName}: could not read the version from '{path}': {e.Message}");
            return null;
        }

        static string? GetString(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    /// <summary>The version that last ran, from <c>BeamNG.drive.ini</c> next to the user folder's parent.</summary>
    private static VersionInfo? ReadIniVersion(string userDir, List<DiscoveryProblem> problems)
    {
        string? productDir = Path.GetDirectoryName(userDir);
        string? companyDir = productDir is null ? null : Path.GetDirectoryName(productDir);
        if (companyDir is null)
        {
            return null;
        }

        string path = Path.Join(companyDir, VersionIniFileName);
        string? text = ReadText(path, problems);
        string? version = text is null ? null : IniFile.ReadValue(text, "version");
        return string.IsNullOrEmpty(version) ? null : new VersionInfo(version, Build: null, VersionIniFileName);
    }

    /// <summary>The version that last ran, from the first line of <c>beamng.log</c>.</summary>
    private static VersionInfo? ReadLogVersion(string userDir, List<DiscoveryProblem> problems)
    {
        string path = Path.Join(userDir, LogFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            // The game keeps the log open while it runs.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            Match match = LogHeaderRegex().Match(reader.ReadLine() ?? string.Empty);
            return match.Success ? new VersionInfo(match.Groups["version"].Value, match.Groups["build"].Value, LogFileName) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.AddWarning($"{GameName}: could not read '{path}': {e.Message}");
            return null;
        }
    }

    private static void CheckVersion(VersionInfo? installed, VersionInfo? lastRun, VersionInfo? version, List<DiscoveryProblem> problems)
    {
        if (version is null)
        {
            problems.AddWarning($"{GameName}: the version could not be determined.");
            return;
        }

        if (System.Version.TryParse(version.Version, out Version? parsed) && parsed < _minimumVersion)
        {
            problems.AddWarning($"{GameName}: version {version.Version} is older than 0.40, the oldest version CsToBeamNG supports.");
        }

        if (installed is not null && lastRun is not null && !string.Equals(installed.Version, lastRun.Version, StringComparison.Ordinal))
        {
            problems.AddInfo($"{GameName}: version {installed.Version} is installed, but the user folder was last used by {lastRun.Version}. Start the game once so that it updates the user folder.");
        }
    }

    /// <summary>Reports user folders in the layout from before 0.37. Current versions don't use them.</summary>
    private static void ReportOldUserFolders(DiscoveryContext context, PathTools paths, SteamInfo steam, List<DiscoveryProblem> problems)
    {
        var dataDirs = new List<string>();
        string? nativeDataDir = context.Os == OsKind.Windows ? context.LocalAppData : paths.DataHome;
        if (!string.IsNullOrEmpty(nativeDataDir))
        {
            dataDirs.Add(nativeDataDir);
        }

        if (context.Os == OsKind.Linux)
        {
            dataDirs.AddRange(ProtonPrefixes.LocalAppDataDirs(steam, AppId));
        }

        foreach (string dataDir in dataDirs)
        {
            string oldRoot = Path.Join(dataDir, ProductDirName);
            if (!Directory.Exists(oldRoot))
            {
                continue;
            }

            foreach (string dir in Directory.EnumerateDirectories(oldRoot).Order(StringComparer.Ordinal))
            {
                if (OldVersionDirRegex().IsMatch(Path.GetFileName(dir)))
                {
                    problems.AddInfo($"{GameName}: an old user folder from before version 0.37 is at '{dir}'. Current versions don't use it.");
                }
            }
        }
    }

    private static string? ReadText(string path, List<DiscoveryProblem> problems)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.AddWarning($"{GameName}: could not read '{path}': {e.Message}");
            return null;
        }
    }

    private static string? FirstNumber(string? text) => text is null ? null : NumberRegex().Match(text) is { Success: true } match ? match.Value : null;

    private static FoundPath Derived(string path, bool isFile) =>
        new(path, LocationSource.Derived, isFile ? File.Exists(path) : Directory.Exists(path));

    // First line of beamng.log, e.g. "0.00040|D|logging| -- Log started - v 0.40.5.0 - x64 - build 302427 - ...".
    [GeneratedRegex(@"Log started - v (?<version>\d+(?:\.\d+)+)\b.*?\bbuild (?<build>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LogHeaderRegex();

    // Pre-0.37 user folders are named after the major and minor version, e.g. "0.36".
    [GeneratedRegex(@"^\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex OldVersionDirRegex();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    private sealed record VersionInfo(string Version, string? Build, string Source);
}
