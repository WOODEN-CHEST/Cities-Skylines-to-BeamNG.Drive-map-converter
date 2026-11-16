using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Cli;

/// <summary>Writes the result of <c>cstobeamng doctor</c> for people to read.</summary>
internal static class DoctorReport
{
    private const int LabelWidth = 16;
    private const string Indent = "  ";

    private static readonly string[] _sizeUnits = ["B", "kB", "MB", "GB", "TB"];

    public static void Write(GameEnvironment environment, TextWriter output, TimeZoneInfo timeZone)
    {
        output.WriteLine($"CsToBeamNG environment check ({environment.Os})");
        output.WriteLine();
        WriteSteam(environment.Steam, output);
        output.WriteLine();
        WriteCitiesSkylines(environment.CitiesSkylines, output, timeZone);
        output.WriteLine();
        WriteBeamNG(environment.BeamNG, output);
        output.WriteLine();
        WriteProblems(environment, output);
    }

    /// <summary>Formats a size with decimal units and the invariant culture, such as "8.6 MB".</summary>
    internal static string FormatSize(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1000 && unit < _sizeUnits.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        string number = unit == 0
            ? bytes.ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{number} {_sizeUnits[unit]}";
    }

    private static void WriteSteam(SteamInfo steam, TextWriter output)
    {
        output.WriteLine("Steam");
        if (steam.Roots.Count == 0)
        {
            WriteField(output, "Folder", "not found");
        }

        foreach (FoundPath root in steam.Roots)
        {
            WriteField(output, "Folder", Describe(root));
        }

        foreach (string library in steam.Libraries)
        {
            WriteField(output, "Library", library);
        }
    }

    private static void WriteCitiesSkylines(CitiesSkylinesInfo game, TextWriter output, TimeZoneInfo timeZone)
    {
        output.WriteLine("Cities: Skylines");
        WriteField(output, "Install", Describe(game.InstallDir, SteamBuild(game.SteamBuildId)));
        WriteField(output, "Managed DLLs", Describe(game.ManagedDir));
        WriteField(output, "User data", Describe(game.UserDir));
        WriteField(output, "Mods", Describe(game.ModsDir));
        string exporter = game.ExporterModVersion is null ? "not deployed" : "version " + game.ExporterModVersion;
        WriteField(output, "Exporter mod", Describe(game.ExporterModDir, exporter, missing: null));
        WriteField(output, "Dumps", Describe(game.DumpRoot, missing: "not created yet"));
        string items = game.WorkshopItemCount.ToString(CultureInfo.InvariantCulture) + (game.WorkshopItemCount == 1 ? " item" : " items");
        WriteField(output, "Workshop", game.WorkshopDir is null ? "none" : Describe(game.WorkshopDir, items));
        WriteField(output, "Game log", Describe(game.LogFile, missing: "not written yet"));
        WriteField(output, "Saves", game.Saves.Count.ToString(CultureInfo.InvariantCulture));
        WriteSaves(game.Saves, output, timeZone);
    }

    private static void WriteSaves(IReadOnlyList<SaveGame> saves, TextWriter output, TimeZoneInfo timeZone)
    {
        int nameWidth = saves.Count == 0 ? 0 : saves.Max(save => save.Name.Length);
        foreach (SaveGame save in saves)
        {
            string written = TimeZoneInfo.ConvertTimeFromUtc(save.LastWriteTimeUtc, timeZone)
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            string where = save.Location == SaveLocation.SteamCloud
                ? $"Steam Cloud, account {save.SteamAccountId}"
                : "local";
            output.WriteLine($"{Indent}{Indent}{save.Name.PadRight(nameWidth)}  {FormatSize(save.SizeBytes),9}  {written}  {where}");
        }
    }

    private static void WriteBeamNG(BeamNGInfo game, TextWriter output)
    {
        output.WriteLine("BeamNG.drive");
        WriteField(output, "Install", Describe(game.InstallDir, SteamBuild(game.SteamBuildId)));
        string version = game.Version is null
            ? "unknown"
            : game.Version + (game.EngineBuild is null ? string.Empty : $", build {game.EngineBuild}") + $" (from {game.VersionSource})";
        WriteField(output, "Version", version);
        WriteField(output, "User folder", Describe(game.UserDir));
        WriteField(output, "Unpacked mods", Describe(game.UnpackedModsDir, missing: "not created yet"));
        WriteField(output, "Game log", Describe(game.LogFile, missing: "not written yet"));
    }

    private static void WriteProblems(GameEnvironment environment, TextWriter output)
    {
        output.WriteLine("Problems");
        if (environment.Problems.Count == 0)
        {
            output.WriteLine($"{Indent}none");
        }

        foreach (DiscoveryProblem problem in environment.Problems)
        {
            string label = problem.Severity switch
            {
                ProblemSeverity.Error => "error:",
                ProblemSeverity.Warning => "warning:",
                _ => "note:",
            };
            output.WriteLine($"{Indent}{label,-9}{problem.Message}");
        }

        output.WriteLine();
        output.WriteLine(environment.HasErrors
            ? "Result: something essential is missing; see the errors above."
            : "Result: all essential locations were found.");
    }

    private static void WriteField(TextWriter output, string label, string value) =>
        output.WriteLine($"{Indent}{(label + ":").PadRight(LabelWidth)}{value}");

    private static string? SteamBuild(string? buildId) => buildId is null ? null : "Steam build " + buildId;

    private static string Describe(FoundPath? location, string? extra = null, string? missing = "missing")
    {
        if (location is null)
        {
            return "not found";
        }

        var notes = new List<string>();
        string? source = location.Source switch
        {
            LocationSource.Override or LocationSource.Registry or LocationSource.GameSetting => "from " + location.Detail,
            LocationSource.SteamLibrary => "Steam library",
            LocationSource.ProtonPrefix => "Proton prefix",
            LocationSource.Default => location.Detail ?? "default",
            _ => null,
        };
        AddIfPresent(notes, source);
        AddIfPresent(notes, extra);
        if (!location.Exists)
        {
            AddIfPresent(notes, missing);
        }

        return notes.Count == 0 ? location.Path : $"{location.Path} ({string.Join(", ", notes)})";
    }

    private static void AddIfPresent(List<string> notes, string? note)
    {
        if (!string.IsNullOrEmpty(note))
        {
            notes.Add(note);
        }
    }
}
