using System;
using System.IO;
using System.Linq;
using System.Text;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Install;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class ModInstallerTests
{
    private const string Mod = "cstobeamng_test";

    [Fact]
    public void InstallsFilesAndListsThem()
    {
        using var temp = new TempFolder();
        string mods = temp.PathOf("mods", "unpacked");

        InstallResult result = ModInstaller.Install(mods, Mod, [Output("/levels/t/info.json", "{}"), Output("/levels/t/main/items.level.json", "x")]);

        Assert.Equal(Path.Join(mods, Mod), result.ModDir);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(3, result.ByteCount);
        Assert.Equal(0, result.ReplacedFileCount);
        Assert.Equal(0, result.RemovedFileCount);
        Assert.Equal("{}", File.ReadAllText(Path.Join(result.ModDir, "levels", "t", "info.json")));
        Assert.Equal(["levels/t/info.json", "levels/t/main/items.level.json"], ModInstaller.ReadPreviousFileList(result.ModDir));
        Assert.Empty(Directory.EnumerateFiles(result.ModDir, "*.cstobeamng-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ReplacesOnlyItsOwnFiles()
    {
        using var temp = new TempFolder();
        string mods = temp.PathOf("mods", "unpacked");
        string modDir = Path.Join(mods, Mod);
        ModInstaller.Install(mods, Mod, [Output("/levels/t/info.json", "1"), Output("/levels/t/old/stale.json", "1"), Output("/levels/t/art/box.dae", "1")]);

        // Files the game or the user added, next to ours.
        File.WriteAllText(Path.Join(modDir, "levels", "t", "art", "box.cdae"), "cache");
        File.WriteAllText(Path.Join(modDir, "notes.txt"), "mine");

        InstallResult result = ModInstaller.Install(mods, Mod, [Output("/levels/t/info.json", "2"), Output("/levels/t/art/box.dae", "2"), Output("/levels/t/new.json", "2")]);

        Assert.Equal(2, result.ReplacedFileCount);
        Assert.Equal(1, result.RemovedFileCount);
        Assert.Equal("2", File.ReadAllText(Path.Join(modDir, "levels", "t", "info.json")));
        Assert.Equal("2", File.ReadAllText(Path.Join(modDir, "levels", "t", "new.json")));
        Assert.False(Directory.Exists(Path.Join(modDir, "levels", "t", "old")));
        Assert.Equal("cache", File.ReadAllText(Path.Join(modDir, "levels", "t", "art", "box.cdae")));
        Assert.Equal("mine", File.ReadAllText(Path.Join(modDir, "notes.txt")));
        Assert.Equal(["levels/t/art/box.dae", "levels/t/info.json", "levels/t/new.json"], ModInstaller.ReadPreviousFileList(modDir));
    }

    [Fact]
    public void RefusesAFolderItDidntCreate()
    {
        using var temp = new TempFolder();
        string mods = temp.PathOf("mods", "unpacked");
        Directory.CreateDirectory(Path.Join(mods, Mod));
        File.WriteAllText(Path.Join(mods, Mod, "info.json"), "theirs");

        Assert.Throws<InstallException>(() => ModInstaller.Install(mods, Mod, [Output("/levels/t/info.json", "{}")]));
        Assert.Equal("theirs", File.ReadAllText(Path.Join(mods, Mod, "info.json")));
    }

    [Fact]
    public void AcceptsAnEmptyFolder()
    {
        using var temp = new TempFolder();
        string mods = temp.PathOf("mods", "unpacked");
        Directory.CreateDirectory(Path.Join(mods, Mod));

        Assert.Equal(1, ModInstaller.Install(mods, Mod, [Output("/levels/t/info.json", "{}")]).FileCount);
    }

    [Theory]
    [InlineData("cheatsAnywhere")]
    [InlineData("hello")]
    [InlineData("cstobeamng_")]
    [InlineData("cstobeamng_Bad")]
    public void InstallsOnlyIntoItsOwnModFolders(string name)
    {
        using var temp = new TempFolder();

        Assert.Throws<ArgumentException>(() => ModInstaller.Install(temp.Root, name, [Output("/a.json", "{}")]));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Root));
    }

    [Fact]
    public void RejectsClashingPaths()
    {
        using var temp = new TempFolder();

        Assert.Throws<ArgumentException>(() => ModInstaller.Install(temp.Root, Mod, [Output("/a/b.json", "1"), Output("/a/B.json", "2")]));
        Assert.Throws<ArgumentException>(() => ModInstaller.Install(temp.Root, Mod, [Output("/a", "1"), Output("/a/b.json", "2")]));
        Assert.Throws<ArgumentException>(() => ModInstaller.Install(temp.Root, Mod, [Output("/" + ModInstaller.FileListName, "1")]));
    }

    [Theory]
    [InlineData("""{"generator":"cstobeamng","files":["../escape.txt"]}""")]
    [InlineData("""{"generator":"cstobeamng","files":["/absolute.txt"]}""")]
    [InlineData("""{"generator":"cstobeamng","files":[3]}""")]
    [InlineData("""{"generator":"someone-else","files":[]}""")]
    [InlineData("""{"files":[]}""")]
    [InlineData("not json")]
    public void RefusesAnInvalidFileList(string list)
    {
        using var temp = new TempFolder();
        string modDir = temp.PathOf(Mod);
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Join(modDir, ModInstaller.FileListName), list);
        File.WriteAllText(temp.PathOf("escape.txt"), "keep");

        Assert.Throws<InstallException>(() => ModInstaller.Install(temp.Root, Mod, [Output("/a.json", "{}")]));
        Assert.True(File.Exists(temp.PathOf("escape.txt")));
    }

    [Fact]
    public void DoesntDeleteThroughLinkedFolders()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Creating symbolic links needs extra rights on Windows.
        }

        using var temp = new TempFolder();
        string mods = temp.PathOf("mods");
        string outside = temp.PathOf("outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Join(outside, "victim.json"), "keep");
        ModInstaller.Install(mods, Mod, [Output("/linked/victim.json", "ours")]);
        string linked = Path.Join(mods, Mod, "linked");
        Directory.Delete(linked, recursive: true);
        Directory.CreateSymbolicLink(linked, outside);

        ModInstaller.Install(mods, Mod, [Output("/other.json", "x")]);

        Assert.Equal("keep", File.ReadAllText(Path.Join(outside, "victim.json")));
    }

    [Fact]
    public void WritesTheSameFileListForTheSameFiles()
    {
        using var temp = new TempFolder();
        OutputFile[] files = [Output("/b.json", "1"), Output("/a/c.json", "2")];

        ModInstaller.Install(temp.PathOf("one"), Mod, files);
        ModInstaller.Install(temp.PathOf("two"), Mod, files.Reverse().ToArray());

        Assert.Equal(
            File.ReadAllBytes(temp.PathOf("one", Mod, ModInstaller.FileListName)),
            File.ReadAllBytes(temp.PathOf("two", Mod, ModInstaller.FileListName)));
        Assert.Equal(
            """
            {
              "generator": "cstobeamng",
              "files": [
                "a/c.json",
                "b.json"
              ]
            }

            """,
            File.ReadAllText(temp.PathOf("one", Mod, ModInstaller.FileListName)));
    }

    private static OutputFile Output(string path, string content) =>
        new(VirtualPath.Parse(path), stream => stream.Write(Encoding.UTF8.GetBytes(content)));

}
