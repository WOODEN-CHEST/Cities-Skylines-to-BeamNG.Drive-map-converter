using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Converter.Tests.Discovery;

/// <summary>
/// Builds fake Steam, Cities: Skylines and BeamNG.drive layouts in a <see cref="FakeTree"/>. The
/// layouts mirror what was observed on real installations (docs/research/game-locations.md).
/// </summary>
internal static class FakeGames
{
    public const string CitiesSkylinesBuildId = "22724702";
    public const string BeamNGBuildId = "26461569";

    /// <summary>Creates a Steam folder whose library list names itself and the other libraries.</summary>
    public static string CreateSteam(string rootDir, params string[] otherLibraries)
    {
        var libraries = new List<string> { rootDir };
        libraries.AddRange(otherLibraries);
        var vdf = new StringBuilder("\"libraryfolders\"\n{\n");
        for (int i = 0; i < libraries.Count; i++)
        {
            Directory.CreateDirectory(Path.Join(libraries[i], "steamapps"));
            vdf.Append('\t').Append('"').Append(i).Append("\"\n\t{\n")
                .Append("\t\t\"path\"\t\t\"").Append(Escape(libraries[i])).Append("\"\n")
                .Append("\t\t\"label\"\t\t\"\"\n")
                .Append("\t}\n");
        }

        vdf.Append("}\n");
        FakeTree.WriteFile(Path.Join(rootDir, "steamapps", "libraryfolders.vdf"), vdf.ToString());
        return rootDir;
    }

    /// <summary>Escapes a path for a VDF string, as Steam does.</summary>
    public static string Escape(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    public static string InstallApp(string library, string appId, string installDirName, string buildId)
    {
        string steamApps = Path.Join(library, "steamapps");
        string manifest =
            $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"installdir\"\t\t\"{installDirName}\"\n\t\"buildid\"\t\t\"{buildId}\"\n}}\n";
        FakeTree.WriteFile(Path.Join(steamApps, $"appmanifest_{appId}.acf"), manifest);
        string installDir = Path.Join(steamApps, "common", installDirName);
        Directory.CreateDirectory(installDir);
        return installDir;
    }

    public static string InstallCitiesSkylines(string library)
    {
        string installDir = InstallApp(library, CitiesSkylinesLocator.AppId, "Cities_Skylines", CitiesSkylinesBuildId);
        FakeTree.WriteFile(Path.Join(installDir, "Cities_Data", "Managed", "Assembly-CSharp.dll"), string.Empty);
        return installDir;
    }

    /// <summary>Installs BeamNG.drive with an <c>integrity.json</c> (unless the version is null) and an empty <c>UserPath</c>.</summary>
    public static string InstallBeamNG(string library, string? version = "0.40.5.0")
    {
        string installDir = InstallApp(library, BeamNGLocator.AppId, "BeamNG.drive", BeamNGBuildId);
        if (version is not null)
        {
            FakeTree.WriteFile(
                Path.Join(installDir, "integrity.json"),
                $"{{\"buildinfo\": \"buildbot 302427 on GitLab\", \"format\": 1, \"integritydata\": [[\"/a.json\", 1, \"00\"]], \"product\": \"BeamNG\", \"version\": \"{version}\"}}");
        }

        FakeTree.WriteFile(Path.Join(installDir, "startup.ini"), "; Location of the user folder.\n;UserPath = .\\\nUserPath =\n");
        return installDir;
    }

    /// <summary>Creates a Cities: Skylines user data folder (with <c>Addons/Mods</c>) in a data folder.</summary>
    public static string CreateCitiesSkylinesUserDir(string dataDir)
    {
        string userDir = Path.Join(dataDir, "Colossal Order", "Cities_Skylines");
        Directory.CreateDirectory(Path.Join(userDir, "Addons", "Mods"));
        return userDir;
    }

    /// <summary>Creates a BeamNG.drive user folder in the layout of 0.37 and later, with the ini that names the last version.</summary>
    public static string CreateBeamNGUserDir(string dataDir, string? lastVersion = "0.40.5.0")
    {
        string userDir = Path.Join(dataDir, "BeamNG", "BeamNG.drive", "current");
        Directory.CreateDirectory(userDir);
        if (lastVersion is not null)
        {
            // Like the real file: a byte order mark, then "version = ...".
            File.WriteAllText(Path.Join(dataDir, "BeamNG", "BeamNG.drive.ini"), $"version = {lastVersion}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        return userDir;
    }

    public static DiscoveryContext LinuxContext(FakeTree tree, DiscoveryOverrides? overrides = null) => new()
    {
        Os = OsKind.Linux,
        HomeDir = tree.Dir("home"),
        WorkingDir = tree.Root,
        Overrides = overrides ?? new DiscoveryOverrides(),
    };

    public static DiscoveryContext WindowsContext(FakeTree tree, IRegistryReader? registry) => new()
    {
        Os = OsKind.Windows,
        HomeDir = tree.Dir("Users", "me"),
        LocalAppData = tree.Dir("Users", "me", "AppData", "Local"),
        ProgramFilesX86 = tree.Dir("Program Files (x86)"),
        WorkingDir = tree.Root,
        Registry = registry,
    };

    /// <summary>The data folder of a Linux context: <c>~/.local/share</c>.</summary>
    public static string LinuxDataDir(FakeTree tree) => tree.Dir("home", ".local", "share");

    /// <summary>All problems as text, for assertion messages.</summary>
    public static string Describe(GameEnvironment environment) =>
        string.Join(Environment.NewLine, environment.Problems);
}
