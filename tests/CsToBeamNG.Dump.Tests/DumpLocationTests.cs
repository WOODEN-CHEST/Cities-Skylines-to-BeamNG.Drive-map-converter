using System;
using System.IO;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class DumpLocationTests
{
    private static readonly DateTime _time = new(2026, 11, 7, 9, 5, 3, DateTimeKind.Local);

    [Fact]
    public void PutsDumpsBelowTheUserDataFolder()
    {
        string userData = Path.Join("home", "Colossal Order", "Cities_Skylines");

        Assert.Equal(Path.Join(userData, "CsToBeamNG", "dumps"), DumpLocation.GetDumpsFolder(userData));
    }

    [Theory]
    [InlineData("CityPalma", "citypalma_20261107-090503")]
    [InlineData("New York  City!", "new-york-city_20261107-090503")]
    [InlineData("  --Seaside 2--  ", "seaside-2_20261107-090503")]
    [InlineData("Łódź", "d_20261107-090503")]
    [InlineData("東京", "city_20261107-090503")]
    [InlineData("", "city_20261107-090503")]
    [InlineData(null, "city_20261107-090503")]
    public void NamesTheFolderAfterTheCityAndTime(string? cityName, string expected)
    {
        Assert.Equal(expected, DumpLocation.GetFolderName(cityName, _time));
    }

    [Fact]
    public void CutsLongCityNamesWithoutATrailingHyphen()
    {
        // 31 letters leave no room for a hyphen and another letter, so the hyphen is dropped too.
        string cityName = new string('a', 31) + " bb";

        string name = DumpLocation.GetFolderName(cityName, _time);

        Assert.Equal(new string('a', 31) + "_20261107-090503", name);
        Assert.Equal(new string('a', 30) + "-b_20261107-090503", DumpLocation.GetFolderName(new string('a', 30) + " bb", _time));
        Assert.Equal(
            new string('a', DumpLocation.MaxCityPartLength),
            DumpLocation.GetFolderName(new string('A', 50), _time).Split('_')[0]);
    }

    [Fact]
    public void UsesOnlyPortableCharacters()
    {
        string name = DumpLocation.GetFolderName("Ä é \"<>|?*:/\\ \t\n x", _time);

        Assert.Equal("x_20261107-090503", name);
        Assert.DoesNotContain(name, c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'));
    }

    [Fact]
    public void FormatsTheTimeTheSameInEveryCulture()
    {
        using var culture = new CultureScope("ar-SA");

        Assert.Equal("citypalma_20261107-090503", DumpLocation.GetFolderName("CityPalma", _time));
    }

    [Fact]
    public void ChoosesAFolderThatDoesNotExistYet()
    {
        using var folder = new TempFolder();
        string first = DumpLocation.ChooseNewFolder(folder.Root, "CityPalma", _time);
        Assert.Equal(folder.PathOf("citypalma_20261107-090503"), first);
        Assert.False(Directory.Exists(first));

        Directory.CreateDirectory(first);
        string second = DumpLocation.ChooseNewFolder(folder.Root, "CityPalma", _time);
        Assert.Equal(folder.PathOf("citypalma_20261107-090503-2"), second);

        // A file with the name is in the way as well.
        File.WriteAllText(second, string.Empty);
        Assert.Equal(folder.PathOf("citypalma_20261107-090503-3"), DumpLocation.ChooseNewFolder(folder.Root, "CityPalma", _time));
    }

    [Fact]
    public void TheChosenFolderSuitsTheDumpWriter()
    {
        using var folder = new TempFolder();
        string dumpFolder = DumpLocation.ChooseNewFolder(DumpLocation.GetDumpsFolder(folder.Root), "CityPalma", _time);

        DumpWriter writer = DumpWriter.Create(dumpFolder, TestManifests.Minimal());
        writer.WriteManifest();

        Assert.True(File.Exists(Path.Join(dumpFolder, DumpFormat.ManifestFileName)));
    }
}
