using System.CommandLine;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class LocationOptionsTests
{
    [Fact]
    public void TurnsOptionsIntoOverridesNamedAfterTheOption()
    {
        var options = new LocationOptions();
        var command = new RootCommand();
        options.AddTo(command);

        ParseResult result = command.Parse(["--cs1-dir", "/games/cs1", "--beamng-user-dir", "~/current"]);
        DiscoveryOverrides overrides = options.GetOverrides(result);

        Assert.Empty(result.Errors);
        Assert.Equal(new PathOverride("/games/cs1", "--cs1-dir"), overrides.CitiesSkylinesDir);
        Assert.Equal(new PathOverride("~/current", "--beamng-user-dir"), overrides.BeamNGUserDir);
        Assert.Null(overrides.SteamDir);
        Assert.Null(overrides.CitiesSkylinesUserDir);
        Assert.Null(overrides.BeamNGDir);
    }
}
