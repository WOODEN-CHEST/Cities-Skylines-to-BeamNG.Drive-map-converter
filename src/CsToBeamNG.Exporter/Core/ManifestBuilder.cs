using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Exporter.Core;

/// <summary>Fills in the dump's manifest from what the exporter read (docs/dump-format.md, manifest.json).</summary>
internal static class ManifestBuilder
{
    /// <summary>The exporter's name in the manifest.</summary>
    public const string ExporterName = "CsToBeamNG.Exporter";

    /// <summary>The name recorded for a mod that reports none.</summary>
    public const string UnnamedMod = "(unnamed mod)";

    /// <summary>
    /// Creates the manifest that the export writes first: who wrote it, the game, the enabled mods and the
    /// city's name, with <see cref="DumpManifest.Complete"/> false.
    /// </summary>
    /// <param name="exporterVersion">The exporter's version.</param>
    /// <param name="gameVersion">The game's version as it reports it.</param>
    /// <param name="mods">The enabled mods. They are sorted, and invalid values are dropped (see <see cref="AddMods"/>).</param>
    /// <param name="cityName">The city's name, or <see langword="null"/>.</param>
    /// <param name="createdUtc">When the export started, in UTC.</param>
    public static DumpManifest CreateInitial(
        string exporterVersion,
        string gameVersion,
        IEnumerable<GameModInfo> mods,
        string? cityName,
        DateTime createdUtc)
    {
        var manifest = new DumpManifest
        {
            Complete = false,
            CreatedUtc = createdUtc,
            Exporter = new ExporterInfo(ExporterName, exporterVersion),
        };
        manifest.Game.Version = gameVersion;
        manifest.City.Name = cityName ?? string.Empty;
        AddMods(manifest.Game.Mods, mods);
        return manifest;
    }

    /// <summary>
    /// Adds what was read from the simulation: the city's facts, its unlocked tiles and the counts. Each
    /// problem is added to the manifest's warnings and returned, for the log.
    /// </summary>
    /// <param name="manifest">The manifest from <see cref="CreateInitial"/>.</param>
    /// <param name="snapshot">What was read from the simulation.</param>
    public static IList<string> AddSnapshot(DumpManifest manifest, CitySnapshot snapshot)
    {
        var problems = new List<string>();
        CityInfo city = manifest.City;
        city.Name = snapshot.CityName;
        city.MapName = NullIfEmpty(snapshot.MapName);
        city.Environment = NullIfEmpty(snapshot.Environment);
        city.MapTheme = NullIfEmpty(snapshot.MapTheme);
        city.LeftHandTraffic = snapshot.LeftHandTraffic;

        IList<TileCoordinate> tiles = AreaGrid.ToTiles(snapshot.AreaGrid, out string? tileProblem);
        city.UnlockedTiles.Clear();
        foreach (TileCoordinate tile in tiles)
        {
            city.UnlockedTiles.Add(tile);
        }

        if (tileProblem is not null)
        {
            problems.Add(tileProblem);
        }

        foreach (ObjectCount count in snapshot.Counts)
        {
            manifest.Counts[count.Key] = count.Created;
        }

        foreach (string problem in problems)
        {
            manifest.Warnings.Add(problem);
        }

        return problems;
    }

    /// <summary>
    /// Adds mods sorted by name and workshop id, so the manifest doesn't depend on the order the game lists
    /// them in. An empty name becomes <see cref="UnnamedMod"/>, and a workshop id that isn't decimal digits
    /// is dropped, because the manifest's writer would reject it.
    /// </summary>
    /// <param name="target">The manifest's list of mods.</param>
    /// <param name="mods">The mods as read from the game.</param>
    public static void AddMods(IList<GameModInfo> target, IEnumerable<GameModInfo> mods)
    {
        var sorted = new List<GameModInfo>();
        foreach (GameModInfo mod in mods)
        {
            string name = string.IsNullOrEmpty(mod.Name) ? UnnamedMod : mod.Name;
            string? workshopId = IsDecimalDigits(mod.WorkshopId) ? mod.WorkshopId : null;
            sorted.Add(new GameModInfo(name, workshopId));
        }

        sorted.Sort(CompareMods);
        foreach (GameModInfo mod in sorted)
        {
            target.Add(mod);
        }
    }

    private static int CompareMods(GameModInfo x, GameModInfo y)
    {
        int byName = string.CompareOrdinal(x.Name, y.Name);
        return byName != 0 ? byName : string.CompareOrdinal(x.WorkshopId, y.WorkshopId);
    }

    // Steam's published file ids are 64-bit numbers, so at most 20 digits.
    private static bool IsDecimalDigits(string? text)
    {
        if (string.IsNullOrEmpty(text) || text!.Length > 20)
        {
            return false;
        }

        foreach (char c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
