using System;
using System.IO;
using System.Linq;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Discovery;

/// <summary>Discovery in simulated Linux layouts. They run on Windows too, except the symlink test.</summary>
public sealed class LinuxDiscoveryTests
{
    [Fact]
    public void FindsEverythingInAStandardLayout()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"));
        string citiesSkylines = FakeGames.InstallCitiesSkylines(steam);
        string beamNG = FakeGames.InstallBeamNG(steam);
        string citiesSkylinesUser = FakeGames.CreateCitiesSkylinesUserDir(dataDir);
        string beamNGUser = FakeGames.CreateBeamNGUserDir(dataDir);
        FakeTree.WriteFile(Path.Join(citiesSkylinesUser, "Saves", "Local City.crp"), "123");
        string remote = Path.Join(steam, "userdata", "12345", CitiesSkylinesLocator.AppId, "remote");
        FakeTree.WriteFile(Path.Join(remote, "Cloud City.crp"), "12345");
        FakeTree.WriteFile(Path.Join(remote, "sharedSettings.cgs"), "not a save");
        tree.Dir("home", ".local", "share", "Steam", "steamapps", "workshop", "content", CitiesSkylinesLocator.AppId, "1234567");

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.False(environment.HasErrors, FakeGames.Describe(environment));
        Assert.Equal(new FoundPath(steam, LocationSource.Default, Exists: true, "via ~/.local/share/Steam"), Assert.Single(environment.Steam.Roots));
        Assert.Equal([steam], environment.Steam.Libraries);

        CitiesSkylinesInfo game = environment.CitiesSkylines;
        Assert.Equal(new FoundPath(citiesSkylines, LocationSource.SteamLibrary, Exists: true, steam), game.InstallDir);
        Assert.Equal(FakeGames.CitiesSkylinesBuildId, game.SteamBuildId);
        Assert.Equal(Path.Join(citiesSkylines, "Cities_Data", "Managed"), game.ManagedDir?.Path);
        Assert.Equal(new FoundPath(citiesSkylinesUser, LocationSource.Default, Exists: true), game.UserDir);
        Assert.Equal(Path.Join(citiesSkylinesUser, "Addons", "Mods"), game.ModsDir?.Path);
        Assert.Equal(Path.Join(citiesSkylinesUser, "CsToBeamNG", "dumps"), game.DumpRoot?.Path);
        Assert.Null(game.ExporterModVersion);
        Assert.Equal(1, game.WorkshopItemCount);
        Assert.Equal(tree.PathOf("home", ".config", "unity3d", "Colossal Order", "Cities_ Skylines", "Player.log"), game.LogFile?.Path);
        Assert.Equal(["Local City", "Cloud City"], game.Saves.Select(save => save.Name));
        Assert.Equal(new SaveGame("Cloud City", Path.Join(remote, "Cloud City.crp"), 5, game.Saves[1].LastWriteTimeUtc, SaveLocation.SteamCloud, "12345"), game.Saves[1]);
        Assert.Equal(SaveLocation.Local, game.Saves[0].Location);

        BeamNGInfo beamNGInfo = environment.BeamNG;
        Assert.Equal(new FoundPath(beamNG, LocationSource.SteamLibrary, Exists: true, steam), beamNGInfo.InstallDir);
        Assert.Equal("0.40.5.0", beamNGInfo.Version);
        Assert.Equal("302427", beamNGInfo.EngineBuild);
        Assert.Equal("integrity.json", beamNGInfo.VersionSource);
        Assert.Equal(new FoundPath(beamNGUser, LocationSource.Default, Exists: true), beamNGInfo.UserDir);
        Assert.Equal(Path.Join(beamNGUser, "mods", "unpacked"), beamNGInfo.UnpackedModsDir?.Path);
        Assert.Equal(Path.Join(beamNGUser, "beamng.log"), beamNGInfo.LogFile?.Path);
    }

    [Fact]
    public void ResolvesASymlinkedSteamFolderToOneRoot()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs extra rights on Windows.");
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string steam = FakeGames.CreateSteam(tree.PathOf("home", ".steam", "debian-installation"));
        Directory.CreateSymbolicLink(tree.PathOf("home", ".steam", "steam"), steam + "/");
        Directory.CreateSymbolicLink(tree.PathOf("home", ".steam", "root"), "debian-installation");

        SteamInfo info = GameEnvironmentLocator.Locate(context).Steam;

        Assert.Equal(new FoundPath(steam, LocationSource.Default, Exists: true, "via ~/.steam/steam"), Assert.Single(info.Roots));
        Assert.Equal([steam], info.Libraries);
    }

    [Fact]
    public void FindsGamesInOtherLibrariesAndReportsMissingOnes()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string secondLibrary = tree.Dir("mnt", "games", "SteamLibrary");
        string missingLibrary = tree.PathOf("mnt", "unplugged", "SteamLibrary");
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"), secondLibrary, missingLibrary);
        Directory.Delete(missingLibrary, recursive: true);
        string citiesSkylines = FakeGames.InstallCitiesSkylines(secondLibrary);
        FakeGames.InstallBeamNG(steam);
        FakeGames.CreateCitiesSkylinesUserDir(dataDir);
        FakeGames.CreateBeamNGUserDir(dataDir);

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.False(environment.HasErrors, FakeGames.Describe(environment));
        Assert.Equal([steam, secondLibrary], environment.Steam.Libraries);
        Assert.Equal(new FoundPath(citiesSkylines, LocationSource.SteamLibrary, Exists: true, secondLibrary), environment.CitiesSkylines.InstallDir);
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Warning && problem.Message.Contains(missingLibrary, StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsEssentialsMissingOnAnEmptyMachine()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.True(environment.HasErrors);
        Assert.Empty(environment.Steam.Roots);
        Assert.Null(environment.CitiesSkylines.InstallDir);
        Assert.Null(environment.BeamNG.InstallDir);
        string expectedUserDir = tree.PathOf("home", ".local", "share", "Colossal Order", "Cities_Skylines");
        Assert.Equal(new FoundPath(expectedUserDir, LocationSource.Default, Exists: false), environment.CitiesSkylines.UserDir);
        Assert.Equal(4, environment.Problems.Count(problem => problem.Severity == ProblemSeverity.Error));
        Assert.Contains(environment.Problems, problem => problem.Message.Contains(expectedUserDir, StringComparison.Ordinal));
        Assert.Contains(environment.Problems, problem => problem.Message.Contains(DiscoveryOverrides.BeamNGDirVariable, StringComparison.Ordinal));
    }

    [Fact]
    public void UsesOverridesAndReportsMissingOverridePaths()
    {
        using var tree = new FakeTree();
        string library = FakeGames.CreateSteam(tree.PathOf("elsewhere", "Steam"));
        string citiesSkylines = FakeGames.InstallCitiesSkylines(library);
        string beamNG = FakeGames.InstallBeamNG(library);
        string citiesSkylinesUser = FakeGames.CreateCitiesSkylinesUserDir(tree.Dir("elsewhere", "data"));
        DiscoveryContext context = FakeGames.LinuxContext(tree, new DiscoveryOverrides
        {
            SteamDir = new PathOverride(library, DiscoveryOverrides.SteamDirVariable),
            CitiesSkylinesUserDir = new PathOverride(citiesSkylinesUser, "--cs1-user-dir"),
            BeamNGUserDir = new PathOverride(tree.PathOf("nowhere"), "--beamng-user-dir"),
        });

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.Equal(new FoundPath(library, LocationSource.Override, Exists: true, DiscoveryOverrides.SteamDirVariable), Assert.Single(environment.Steam.Roots));
        Assert.Equal(citiesSkylines, environment.CitiesSkylines.InstallDir?.Path);
        Assert.Equal(beamNG, environment.BeamNG.InstallDir?.Path);
        Assert.Equal(new FoundPath(citiesSkylinesUser, LocationSource.Override, Exists: true, "--cs1-user-dir"), environment.CitiesSkylines.UserDir);
        Assert.Equal(new FoundPath(tree.PathOf("nowhere"), LocationSource.Override, Exists: false, "--beamng-user-dir"), environment.BeamNG.UserDir);
        DiscoveryProblem error = Assert.Single(environment.Problems, problem => problem.Severity == ProblemSeverity.Error);
        Assert.Contains("--beamng-user-dir", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PrefersTheNativeBeamNGUserFolderAndNotesOthers()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"));
        FakeGames.InstallBeamNG(steam);
        string native = FakeGames.CreateBeamNGUserDir(dataDir);
        string prefixData = Path.Join(steam, "steamapps", "compatdata", BeamNGLocator.AppId, "pfx", "drive_c", "users", "steamuser", "AppData", "Local");
        string proton = FakeGames.CreateBeamNGUserDir(prefixData, lastVersion: "0.38.3.0");
        string old = Directory.CreateDirectory(Path.Join(prefixData, "BeamNG.drive", "0.36")).FullName;

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.Equal(new FoundPath(native, LocationSource.Default, Exists: true), environment.BeamNG.UserDir);
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Info && problem.Message.Contains(proton, StringComparison.Ordinal));
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Info && problem.Message.Contains(old, StringComparison.Ordinal));
    }

    [Fact]
    public void UsesTheProtonUserFolderWhenItIsTheOnlyOne()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string steam = FakeGames.CreateSteam(Path.Join(FakeGames.LinuxDataDir(tree), "Steam"));
        FakeGames.InstallBeamNG(steam);
        string prefixData = Path.Join(steam, "steamapps", "compatdata", BeamNGLocator.AppId, "pfx", "drive_c", "users", "steamuser", "AppData", "Local");
        string proton = FakeGames.CreateBeamNGUserDir(prefixData);

        BeamNGInfo info = GameEnvironmentLocator.Locate(context).BeamNG;

        Assert.Equal(new FoundPath(proton, LocationSource.ProtonPrefix, Exists: true, "Proton prefix"), info.UserDir);
    }

    [Fact]
    public void FollowsUserPathInStartupIni()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"));
        string beamNG = FakeGames.InstallBeamNG(steam);
        FakeGames.CreateBeamNGUserDir(dataDir);
        string moved = tree.Dir("games", "BeamNG user", "current");
        FakeTree.WriteFile(Path.Join(beamNG, "startup.ini"), $"; comment\nUserPath = {Path.GetDirectoryName(moved)}\n");

        BeamNGInfo info = GameEnvironmentLocator.Locate(context).BeamNG;

        Assert.Equal(new FoundPath(moved, LocationSource.GameSetting, Exists: true, "startup.ini UserPath"), info.UserDir);
    }

    [Fact]
    public void ReadsTheVersionFromTheIniOrTheLogWithoutIntegrityJson()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"));
        FakeGames.InstallBeamNG(steam, version: null);
        string user = FakeGames.CreateBeamNGUserDir(dataDir, lastVersion: "0.40.4.0");
        FakeTree.WriteFile(Path.Join(user, "beamng.log"), "  0.00040|D|logging| -- Log started - v 0.40.3.0 - x64 - build 299999 - 2026-11-15 14:09:19 +0200 -----\n");

        BeamNGInfo fromIni = GameEnvironmentLocator.Locate(context).BeamNG;
        File.Delete(Path.Join(dataDir, "BeamNG", "BeamNG.drive.ini"));
        BeamNGInfo fromLog = GameEnvironmentLocator.Locate(context).BeamNG;

        Assert.Equal("0.40.4.0", fromIni.Version);
        Assert.Null(fromIni.EngineBuild);
        Assert.Equal("BeamNG.drive.ini", fromIni.VersionSource);
        Assert.Equal("0.40.3.0", fromLog.Version);
        Assert.Equal("299999", fromLog.EngineBuild);
        Assert.Equal("beamng.log", fromLog.VersionSource);
    }

    [Fact]
    public void ReportsOldVersionsAndAnOutdatedUserFolder()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.LinuxContext(tree);
        string dataDir = FakeGames.LinuxDataDir(tree);
        string steam = FakeGames.CreateSteam(Path.Join(dataDir, "Steam"));
        FakeGames.InstallBeamNG(steam, version: "0.39.4.0");
        FakeGames.CreateBeamNGUserDir(dataDir, lastVersion: "0.38.4.0");

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.Equal("0.39.4.0", environment.BeamNG.Version);
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Warning && problem.Message.Contains("older than 0.40", StringComparison.Ordinal));
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Info && problem.Message.Contains("last used by 0.38.4.0", StringComparison.Ordinal));
    }
}
