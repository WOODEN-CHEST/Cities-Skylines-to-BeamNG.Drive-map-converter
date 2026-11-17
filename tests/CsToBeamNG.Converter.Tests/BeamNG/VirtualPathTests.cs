using System;
using System.IO;
using CsToBeamNG.Converter.BeamNG;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class VirtualPathTests
{
    [Fact]
    public void CombinesSegmentsWithSlashes()
    {
        VirtualPath path = VirtualPath.Parse("/levels/x").Combine("art/terrains/main.materials.json");

        Assert.Equal("/levels/x/art/terrains/main.materials.json", path.Value);
        Assert.Equal(["levels", "x", "art", "terrains", "main.materials.json"], path.Segments);
        Assert.Equal("main.materials.json", path.Name);
        Assert.Equal("/levels/x/art/terrains", path.Parent!.Value);
        Assert.Equal(path.Value, path.ToString());
    }

    [Fact]
    public void RootIsASlashWithoutSegments()
    {
        Assert.Equal("/", VirtualPath.Root.Value);
        Assert.Empty(VirtualPath.Root.Segments);
        Assert.Null(VirtualPath.Root.Parent);
        Assert.Same(VirtualPath.Root, VirtualPath.Parse("/"));
    }

    [Fact]
    public void BuildsALevelFolder() => Assert.Equal("/levels/my_level", VirtualPath.ForLevel("my_level").Value);

    [Theory]
    [InlineData("My_Level")]
    [InlineData("my level")]
    [InlineData("")]
    public void RejectsLevelNamesThatArentGeneratedNames(string name) =>
        Assert.Throws<ArgumentException>(() => VirtualPath.ForLevel(name));

    [Theory]
    [InlineData("")]
    [InlineData("levels/x")]
    [InlineData("/levels//x")]
    [InlineData("/levels/x/")]
    [InlineData("/levels/../x")]
    [InlineData("/levels/./x")]
    [InlineData("/levels\\x")]
    [InlineData("/levels/a:b")]
    [InlineData("/levels/a\tb")]
    public void RejectsMalformedPaths(string value) => Assert.Throws<ArgumentException>(() => VirtualPath.Parse(value));

    [Fact]
    public void KeepsTheGamesSpellingOfReferencedPaths() =>
        Assert.Equal("/levels/Utah/art/skies/clouds fluffy.dds", VirtualPath.Parse("/levels/Utah/art/skies/clouds fluffy.dds").Value);

    [Fact]
    public void MapsToTheFileSystemWithTheOsSeparator() =>
        Assert.Equal(
            Path.Join("mods", "x", "levels", "y", "info.json"),
            VirtualPath.Parse("/levels/y/info.json").ToFileSystemPath(Path.Join("mods", "x")));

    [Fact]
    public void KnowsWhatLiesBelowAFolder()
    {
        VirtualPath folder = VirtualPath.Parse("/levels/x");
        VirtualPath file = folder.Combine("art/box.dae");

        Assert.True(file.IsWithin(folder));
        Assert.True(folder.IsWithin(folder));
        Assert.False(VirtualPath.Parse("/levels/xy/art").IsWithin(folder));
        Assert.Equal("art/box.dae", file.RelativeTo(folder));
        Assert.Throws<ArgumentException>(() => folder.RelativeTo(file));
        Assert.Throws<ArgumentException>(() => folder.RelativeTo(folder));
    }

    [Fact]
    public void ComparesOrdinally()
    {
        Assert.Equal(VirtualPath.Parse("/a/b"), VirtualPath.Root.Combine("a/b"));
        Assert.Equal(VirtualPath.Parse("/a/b").GetHashCode(), VirtualPath.Root.Combine("a").Combine("b").GetHashCode());
        Assert.NotEqual(VirtualPath.Parse("/a/B"), VirtualPath.Parse("/a/b"));
    }
}
