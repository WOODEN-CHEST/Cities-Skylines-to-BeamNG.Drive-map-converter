using CsToBeamNG.Converter.BeamNG;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class BeamNGNamesTests
{
    [Theory]
    [InlineData("cstobeamng_hello", true)]
    [InlineData("terrain.terrain.json", true)]
    [InlineData("a-b_c.d", true)]
    [InlineData("0level", true)]
    [InlineData("Hello", false)]
    [InlineData("hello world", false)]
    [InlineData(".hidden", false)]
    [InlineData("trailing.", false)]
    [InlineData("café", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ChecksGeneratedNames(string? name, bool valid) => Assert.Equal(valid, BeamNGNames.IsGeneratedName(name));

    [Fact]
    public void LimitsTheLengthOfGeneratedNames()
    {
        Assert.True(BeamNGNames.IsGeneratedName(new string('a', BeamNGNames.MaxGeneratedNameLength)));
        Assert.False(BeamNGNames.IsGeneratedName(new string('a', BeamNGNames.MaxGeneratedNameLength + 1)));
    }

    [Theory]
    [InlineData("MissionGroup", true)]
    [InlineData("theLevelInfo", true)]
    [InlineData("spawn_default", true)]
    [InlineData("_hidden", true)]
    [InlineData("2fast", false)]
    [InlineData("with-dash", false)]
    [InlineData("with space", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ChecksSceneNames(string? name, bool valid) => Assert.Equal(valid, BeamNGNames.IsSceneName(name));

    [Theory]
    [InlineData("cstobeamng_hello", true)]
    [InlineData("cstobeamng_", false)]
    [InlineData("cheatsAnywhere", false)]
    [InlineData("unlimitedboost", false)]
    [InlineData("CsToBeamNG_hello", false)]
    public void RecognisesOnlyItsOwnModFolders(string name, bool own) => Assert.Equal(own, BeamNGNames.IsOwnModFolderName(name));
}
