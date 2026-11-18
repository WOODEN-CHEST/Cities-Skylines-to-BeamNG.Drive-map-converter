using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>
/// What a conversion made of a city and what it left out, for the user. <see cref="ToText"/> gives the text that is
/// installed with the level; it depends only on the dump and the options, so it is the same for the same input.
/// </summary>
public sealed class ConversionReport
{
    /// <summary>The name of the file installed with the level, in the level folder.</summary>
    public const string FileName = "cstobeamng-report.txt";

    /// <summary>The level's name.</summary>
    public required string LevelName { get; init; }

    /// <summary>The dump folder's name.</summary>
    public required string DumpName { get; init; }

    /// <summary>The city's name.</summary>
    public required string CityName { get; init; }

    /// <summary>The map's name, or an empty string.</summary>
    public required string MapName { get; init; }

    /// <summary>The game version that exported the dump.</summary>
    public required string GameVersion { get; init; }

    /// <summary>When the dump was exported, in UTC.</summary>
    public required DateTime ExportedUtc { get; init; }

    /// <summary>The part of the map that was converted.</summary>
    public required MapRect Area { get; init; }

    /// <summary>Where <see cref="Area"/> came from, such as "the sections' content plus a 500 m margin".</summary>
    public required string AreaSource { get; init; }

    /// <summary>The terrain's grid.</summary>
    public required TerrainGrid Grid { get; init; }

    /// <summary>The heights the terrain stores.</summary>
    public required TerrainHeightRange HeightRange { get; init; }

    /// <summary>The lowest terrain height, in metres.</summary>
    public required double LowestHeight { get; init; }

    /// <summary>The highest terrain height, in metres.</summary>
    public required double HighestHeight { get; init; }

    /// <summary>The share of samples whose height came from the game's 4 m detail heights, from 0 to 1.</summary>
    public required double DetailShare { get; init; }

    /// <summary>How the 16 m heights were read elsewhere.</summary>
    public required CoarseInterpolation CoarseInterpolation { get; init; }

    /// <summary>The number of terrain edits applied.</summary>
    public required int EditCount { get; init; }

    /// <summary>The number of samples the edits changed.</summary>
    public required long EditedSamples { get; init; }

    /// <summary>
    /// The number of samples of lake beds raised above the sea, so that the sea plane doesn't show through a lake
    /// (<see cref="LakeBedEdits"/>).
    /// </summary>
    public required long RaisedLakeBedSamples { get; init; }

    /// <summary>The number of samples of each <see cref="SurfaceKind"/>, indexed by the kind's value.</summary>
    public required IReadOnlyList<long> SurfaceCounts { get; init; }

    /// <summary>The sorted water, or <see langword="null"/> if the dump has none.</summary>
    public required WaterBodies? Water { get; init; }

    /// <summary>The number of water blocks made for each of <see cref="WaterBodies.Lakes"/>.</summary>
    public required IReadOnlyList<int> LakeBlockCounts { get; init; }

    /// <summary>The spawn point's name.</summary>
    public required string SpawnName { get; init; }

    /// <summary>Where the player spawns, in BeamNG coordinates.</summary>
    public required Vector3 SpawnPosition { get; init; }

    /// <summary>Whether the spawn point stands on dry, level ground; false if none was found and the middle was used.</summary>
    public required bool SpawnOnDryGround { get; init; }

    /// <summary>What the city has that the level doesn't show yet, such as "buildings 2168", in a fixed order.</summary>
    public required IReadOnlyList<string> NotConverted { get; init; }

    /// <summary>Warnings and notes from loading the dump and from the conversion.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>Returns the report as text, lines separated by <c>\n</c>.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        void Line(FormattableString line) => text.Append(line.ToString(CultureInfo.InvariantCulture)).Append('\n');

        Line($"CsToBeamNG conversion report");
        Line($"");
        Line($"Level:   {LevelName}");
        Line($"City:    {CityName}{(MapName.Length == 0 ? string.Empty : " on the map " + MapName)}, exported {ExportedUtc:yyyy-MM-dd HH:mm:ss} UTC by game {GameVersion}");
        Line($"Dump:    {DumpName}");
        Line($"Area:    {Area} ({Area.Width:0} × {Area.Depth:0} m), {AreaSource}");
        Line($"");
        Line($"Terrain");
        Line($"  Grid:      {Grid.Size} × {Grid.Size} samples, {Grid.SquareSize:0.###} m apart, {Grid.ExtentMeters:0.###} m across, covering {Grid.Coverage}");
        if (!Grid.CoversArea)
        {
            Line($"             The area is larger than the largest terrain allowed; the terrain covers its middle.");
        }

        Line($"  Block:     lower-left corner ({Grid.BeamNGPosition.X:0.###}, {Grid.BeamNGPosition.Y:0.###}, {HeightRange.ZOffset:0.###}) in BeamNG, maxHeight {HeightRange.MaxHeight:0.###} m, height step {HeightRange.StepMeters * 1000:0.##} mm");
        Line($"  Heights:   {LowestHeight:0.##} to {HighestHeight:0.##} m; the game's 4 m detail heights at {DetailShare * 100:0.#}% of the samples, its 16 m heights {(CoarseInterpolation == CoarseInterpolation.Bilinear ? "read bilinearly" : "smoothed")} elsewhere");
        Line($"  Edits:     {(EditCount == 0 ? "none" : string.Create(CultureInfo.InvariantCulture, $"{EditCount} stamps changed {EditedSamples} samples"))}");
        if (RaisedLakeBedSamples > 0)
        {
            Line($"             {RaisedLakeBedSamples} samples of lake beds below the sea raised to {LakeBedEdits.ClearanceMeters:0.##} m above it, so the sea plane doesn't show through the lakes");
        }
        Line($"  Materials: {DescribeSurfaces()}");
        Line($"");
        Line($"Water");
        WriteWater(Line);
        Line($"");
        Line($"Spawn point: {SpawnName} at ({SpawnPosition.X:0.##}, {SpawnPosition.Y:0.##}, {SpawnPosition.Z:0.##}), {(SpawnOnDryGround ? "the dry, level ground nearest the area's middle" : "the area's middle; no dry, level ground was found")}");
        Line($"");
        Line($"Not converted yet: {(NotConverted.Count == 0 ? "nothing" : string.Join(", ", NotConverted))}");
        Line($"");
        Line($"Notes");
        if (Notes.Count == 0)
        {
            Line($"  none");
        }

        foreach (string note in Notes)
        {
            Line($"  {note}");
        }

        return text.ToString();
    }

    private string DescribeSurfaces()
    {
        long total = 0;
        foreach (long count in SurfaceCounts)
        {
            total += count;
        }

        var parts = new List<string>();
        foreach (SurfaceKind kind in Enum.GetValues<SurfaceKind>())
        {
            long count = SurfaceCounts[(int)kind];
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{kind.ToString().ToLowerInvariant()} {(total == 0 ? 0 : count * 100.0 / total):0.##}%"));
        }

        return string.Join(", ", parts);
    }

    private void WriteWater(Action<FormattableString> line)
    {
        if (Water is null)
        {
            line($"  The dump has no water.");
            return;
        }

        double km2 = Water.CellAreaSquareMeters / 1e6;
        if (Water.SeaSurface is double sea)
        {
            double drawnOffset = CityWater.DrawnOffsetUnits * CityWater.MetersPerUnit;
            line($"  Sea:       a water plane at {sea:0.###} m (the sea level {Water.SeaLevel:0.###} m, drawn {drawnOffset:0.####} m lower); {Water.SeaCells} cells ({Water.SeaCells * km2:0.##} km²) in the area");
            if (Water.DryCellsBelowSea == 1)
            {
                line($"             1 dry cell ({km2:0.###} km²) lies below the sea and is flooded; the game keeps it dry, probably because a building stands there");
            }
            else if (Water.DryCellsBelowSea > 1)
            {
                line($"             {Water.DryCellsBelowSea} dry cells ({Water.DryCellsBelowSea * km2:0.###} km²) lie below the sea and are flooded; the game keeps them dry, mostly because buildings stand there");
            }
        }
        else
        {
            line($"  Sea:       none (no water at the sea level {Water.SeaLevel:0.###} m along the map's edges)");
        }

        line($"  Lakes:     {Water.Lakes.Count}");
        for (int n = 0; n < Water.Lakes.Count; n++)
        {
            Lake lake = Water.Lakes[n];
            line($"    lake {n + 1}: surface {lake.Surface:0.###} m, {lake.CellCount} cells ({lake.AreaSquareMeters / 1e6:0.###} km²), {LakeBlockCounts[n]} water {(LakeBlockCounts[n] == 1 ? "block" : "blocks")} within {lake.Bounds}, down to {lake.Bottom:0.#} m");
        }

        if (Water.SkippedBodies == 0)
        {
            line($"  Left out:  no rivers or other sloping water");
        }
        else
        {
            line($"  Left out:  {Water.SkippedBodies} {(Water.SkippedBodies == 1 ? "body" : "bodies")} of sloping water such as rivers (Phase 4), {Water.SkippedCells} cells ({Water.SkippedCells * km2:0.##} km²), drawn {Water.SkippedLowest:0.##} to {Water.SkippedHighest:0.##} m");
        }
        if (Water.SmallWaterCells > 0)
        {
            line($"             {Water.SmallWaterCells} cells of small water: ponds, and wave crests of the sea");
        }
    }
}
