using System;
using System.IO;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Discovery;

/// <summary>
/// Discovery in simulated Windows layouts, on any OS: the registry is faked and every Windows
/// folder lives in the fake tree.
/// </summary>
public sealed class WindowsDiscoveryTests
{
    [Fact]
    public void FindsSteamThroughTheRegistry()
    {
        using var tree = new FakeTree();
        string steam = FakeGames.CreateSteam(tree.PathOf("Games", "Steam"));
        var registry = new FakeRegistry().Set(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath", steam.Replace('\\', '/'));
        DiscoveryContext context = FakeGames.WindowsContext(tree, registry);
        string citiesSkylines = FakeGames.InstallCitiesSkylines(steam);
        FakeGames.InstallBeamNG(steam);
        string localAppData = context.LocalAppData!;
        string citiesSkylinesUser = FakeGames.CreateCitiesSkylinesUserDir(localAppData);
        string beamNGUser = FakeGames.CreateBeamNGUserDir(localAppData);

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.False(environment.HasErrors, FakeGames.Describe(environment));
        FoundPath root = Assert.Single(environment.Steam.Roots);
        Assert.True(string.Equals(steam, root.Path, StringComparison.OrdinalIgnoreCase), root.Path);
        Assert.Equal(LocationSource.Registry, root.Source);
        Assert.Equal(@"HKCU\Software\Valve\Steam\SteamPath", root.Detail);
        Assert.Equal(citiesSkylinesUser, environment.CitiesSkylines.UserDir?.Path);
        Assert.Equal(beamNGUser, environment.BeamNG.UserDir?.Path);

        // The Windows build writes Unity's log next to its data.
        Assert.Equal(Path.Join(citiesSkylines, "Cities_Data", "output_log.txt"), environment.CitiesSkylines.LogFile?.Path, ignoreCase: true);
    }

    [Fact]
    public void FallsBackToProgramFilesWithoutRegistryEntries()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.WindowsContext(tree, new FakeRegistry());
        string steam = FakeGames.CreateSteam(Path.Join(context.ProgramFilesX86!, "Steam"));

        SteamInfo info = GameEnvironmentLocator.Locate(context).Steam;

        Assert.Equal(new FoundPath(steam, LocationSource.Default, Exists: true), Assert.Single(info.Roots));
    }

    [Fact]
    public void ReportsTheUserFolderLayoutFromBeforeBeamNG037()
    {
        using var tree = new FakeTree();
        DiscoveryContext context = FakeGames.WindowsContext(tree, registry: null);
        string steam = FakeGames.CreateSteam(Path.Join(context.ProgramFilesX86!, "Steam"));
        FakeGames.InstallBeamNG(steam);
        string old = tree.Dir("Users", "me", "AppData", "Local", "BeamNG.drive", "0.36");

        GameEnvironment environment = GameEnvironmentLocator.Locate(context);

        Assert.False(environment.BeamNG.UserDir?.Exists);
        Assert.Equal(Path.Join(context.LocalAppData!, "BeamNG", "BeamNG.drive", "current"), environment.BeamNG.UserDir?.Path);
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Error && problem.Message.Contains(DiscoveryOverrides.BeamNGUserDirVariable, StringComparison.Ordinal));
        Assert.Contains(environment.Problems, problem =>
            problem.Severity == ProblemSeverity.Info && problem.Message.Contains(old, StringComparison.Ordinal));
    }
}
