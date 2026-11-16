using System.Collections.Generic;
using System.IO;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Discovery;

public sealed class DiscoveryOverridesTests
{
    [Fact]
    public void ReadsTheEnvironmentAndLetsTheCommandLineWin()
    {
        var environment = new Dictionary<string, string>
        {
            [DiscoveryOverrides.CitiesSkylinesDirVariable] = " /games/cs1 ",
            [DiscoveryOverrides.BeamNGUserDirVariable] = "/beamng/current",
            [DiscoveryOverrides.SteamDirVariable] = "   ",
        };
        DiscoveryOverrides fromEnvironment = DiscoveryOverrides.FromEnvironment(name => environment.GetValueOrDefault(name));
        var fromCommandLine = new DiscoveryOverrides { BeamNGUserDir = new PathOverride("/elsewhere", "--beamng-user-dir") };

        DiscoveryOverrides combined = fromEnvironment.OverriddenBy(fromCommandLine);

        Assert.Equal(new PathOverride("/games/cs1", DiscoveryOverrides.CitiesSkylinesDirVariable), combined.CitiesSkylinesDir);
        Assert.Equal(new PathOverride("/elsewhere", "--beamng-user-dir"), combined.BeamNGUserDir);
        Assert.Null(combined.SteamDir);
        Assert.Null(combined.BeamNGDir);
    }

    [Fact]
    public void ResolvesHomeAndRelativeOverridePaths()
    {
        using var tree = new FakeTree();
        DiscoveryContext baseContext = FakeGames.LinuxContext(tree);
        string install = tree.Dir("home", "games", "Cities_Skylines");
        string user = tree.Dir("relative", "cs1-user");
        DiscoveryContext context = baseContext with
        {
            Overrides = new DiscoveryOverrides
            {
                CitiesSkylinesDir = new PathOverride("~/games/Cities_Skylines/", "--cs1-dir"),
                CitiesSkylinesUserDir = new PathOverride(Path.Join("relative", "cs1-user"), "--cs1-user-dir"),
            },
        };

        CitiesSkylinesInfo game = GameEnvironmentLocator.Locate(context).CitiesSkylines;

        Assert.Equal(new FoundPath(install, LocationSource.Override, Exists: true, "--cs1-dir"), game.InstallDir);
        Assert.Equal(new FoundPath(user, LocationSource.Override, Exists: true, "--cs1-user-dir"), game.UserDir);
    }
}
