using System;
using System.Collections.Generic;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class SaveMatcherTests
{
    private static readonly DateTime _older = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _newer = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PrefersAnExactMatchOverOneThatIgnoresCase()
    {
        List<SaveCandidate> saves =
        [
            new("citypalma", "citypalma", _newer),
            new("CityPalma", "CityPalma", _older),
        ];

        Assert.Equal(1, SaveMatcher.FindBest(saves, "CityPalma"));
        Assert.Equal(0, SaveMatcher.FindBest(saves, "citypalma"));
        Assert.Equal(0, SaveMatcher.FindBest(saves, "CITYPALMA"));
    }

    [Fact]
    public void TakesTheNewestOfSavesWithTheSameName()
    {
        List<SaveCandidate> saves =
        [
            new("Rockville", "Rockville", _older),
            new("Rockville", "Rockville", _newer),
            new("Other", "Other", _newer),
        ];

        Assert.Equal(1, SaveMatcher.FindBest(saves, "Rockville"));
    }

    [Fact]
    public void MatchesTheFileNameToo()
    {
        List<SaveCandidate> saves = [new("Listed name", "file-name", _older)];

        Assert.Equal(0, SaveMatcher.FindBest(saves, "file-name"));
        Assert.Equal(-1, SaveMatcher.FindBest(saves, "missing"));
    }

    [Theory]
    [InlineData("cloud:CityPalma.crp", "CityPalma")]
    [InlineData("/home/user/.local/share/Colossal Order/Cities_Skylines/Saves/My City.crp", "My City")]
    [InlineData("C:\\Users\\me\\AppData\\Local\\Colossal Order\\Cities_Skylines\\Saves\\Town.v2.crp", "Town.v2")]
    [InlineData("NoExtension", "NoExtension")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("cloud:", null)]
    public void TakesTheFileNameFromAPackagePath(string? packagePath, string? expected)
    {
        Assert.Equal(expected, SaveCandidate.FileNameFromPackagePath(packagePath));
    }

    [Fact]
    public void ListsTheSavesWhenNoneMatches()
    {
        List<SaveCandidate> saves =
        [
            new("Rockville", null, _older),
            new("alpha", null, _older),
            new("Rockville", null, _newer),
        ];

        Assert.Equal(
            "The game has no save named \"Palma\". Its saves are: \"alpha\", \"Rockville\".",
            SaveMatcher.DescribeNotFound(saves, "Palma"));
        Assert.Equal("The game has no saves, so \"Palma\" can't be loaded.", SaveMatcher.DescribeNotFound([], "Palma"));
    }

    [Fact]
    public void ShortensALongListOfSaves()
    {
        var saves = new List<SaveCandidate>();
        for (int i = 0; i < 25; i++)
        {
            saves.Add(new SaveCandidate("save" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture), null, _older));
        }

        string message = SaveMatcher.DescribeNotFound(saves, "x");

        Assert.Contains("\"save19\", ...", message, StringComparison.Ordinal);
        Assert.DoesNotContain("save20", message, StringComparison.Ordinal);
    }
}
