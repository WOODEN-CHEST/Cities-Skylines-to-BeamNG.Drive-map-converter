using System;
using CsToBeamNG.Converter.Conversion;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Conversion;

public sealed class LevelNamesTests
{
    [Theory]
    [InlineData("San Palma", "cstobeamng_san_palma")]
    [InlineData("  São Paulo!! 2 ", "cstobeamng_sao_paulo_2")]
    [InlineData("Rockyvalley", "cstobeamng_rockyvalley")]
    [InlineData("", "cstobeamng_city")]
    [InlineData("東京", "cstobeamng_city")]
    [InlineData(null, "cstobeamng_city")]
    public void MakesNamesFromCityNames(string? cityName, string expected)
    {
        string name = LevelNames.FromCityName(cityName);

        Assert.Equal(expected, name);
        Assert.True(LevelNames.IsLevelName(name));
    }

    [Fact]
    public void ShortensLongCityNames()
    {
        string name = LevelNames.FromCityName(new string('x', 200));

        Assert.Equal(64, name.Length);
        Assert.True(LevelNames.IsLevelName(name));
    }

    [Theory]
    [InlineData("palma", "cstobeamng_palma")]
    [InlineData("cstobeamng_palma", "cstobeamng_palma")]
    [InlineData("palma_2", "cstobeamng_palma_2")]
    public void PrefixesUserNames(string name, string expected) => Assert.Equal(expected, LevelNames.FromUserName(name));

    [Theory]
    [InlineData("Palma")]
    [InlineData("san-palma")]
    [InlineData("san.palma")]
    [InlineData("")]
    public void RejectsUserNamesThatArentLowercaseLettersDigitsAndUnderscores(string name) =>
        Assert.Throws<ArgumentException>(() => LevelNames.FromUserName(name));
}
