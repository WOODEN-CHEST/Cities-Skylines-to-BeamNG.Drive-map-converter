using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Cli;

/// <summary>Writes the result of <c>cstobeamng inspect</c> for people to read.</summary>
internal static class InspectReport
{
    private const int LabelWidth = 16;
    private const string Indent = "  ";

    public static void Write(WorldLoadResult loaded, AreaOfInterest areaOfInterest, TextWriter output)
    {
        DumpManifest manifest = loaded.World.Manifest;
        output.WriteLine("City dump " + loaded.World.DumpFolder);
        output.WriteLine();
        WriteManifest(manifest, output);
        output.WriteLine();
        WriteCounts(manifest, output);
        output.WriteLine();
        WriteSections(loaded, output);
        output.WriteLine();
        WriteAreaOfInterest(areaOfInterest, output);
        output.WriteLine();
        WriteWarnings(manifest, loaded.Messages, output);
    }

    public static void WriteRender(string path, RenderArea area, RenderView view, IReadOnlyList<IRenderLayer> layers, TextWriter output)
    {
        output.WriteLine("Render");
        WriteField(output, "File", path);
        string bounds = view.Area.ToString();
        WriteField(output, "Area", area.Description == bounds ? bounds : $"{area.Description}: {bounds}");
        WriteField(output, "Image", string.Create(
            CultureInfo.InvariantCulture,
            $"{view.Width} × {view.Height} pixels, {view.MetersPerPixel:G4} m per pixel; +X (east) right, +Z (north) up"));
        WriteField(output, "Layers", "bottom to top");
        foreach (IRenderLayer layer in layers)
        {
            output.WriteLine($"{Indent}{Indent}{layer.Name}: {layer.Description}");
        }
    }

    private static void WriteManifest(DumpManifest manifest, TextWriter output)
    {
        output.WriteLine("Manifest");
        WriteField(output, "Format", manifest.FormatVersion + (manifest.Complete ? ", complete" : ", INCOMPLETE: the export didn't finish"));
        WriteField(output, "Exported", string.Create(
            CultureInfo.InvariantCulture,
            $"{manifest.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC by {manifest.Exporter.Name} {manifest.Exporter.Version}"));
        WriteField(output, "Game", manifest.Game.Version);
        WriteField(output, "Mods", manifest.Game.Mods.Count == 0 ? "none" : manifest.Game.Mods.Count.ToString(CultureInfo.InvariantCulture));
        foreach (GameModInfo mod in manifest.Game.Mods)
        {
            output.WriteLine($"{Indent}{Indent}{mod.Name}{(mod.WorkshopId is null ? string.Empty : $" (workshop item {mod.WorkshopId})")}");
        }

        CityInfo city = manifest.City;
        WriteField(output, "City", city.Name.Length == 0 ? "(no name)" : city.Name);
        WriteField(output, "Map", city.MapName ?? "unknown");
        WriteField(output, "Environment", city.Environment ?? "unknown");
        WriteField(output, "Map theme", city.MapTheme ?? "none");
        WriteField(output, "Traffic", city.LeftHandTraffic ? "left-hand" : "right-hand");
        WriteField(output, "Unlocked tiles", city.UnlockedTiles.Count == 0
            ? "none recorded"
            : string.Join(", ", city.UnlockedTiles.Select(tile => tile.ToString())));
    }

    private static void WriteCounts(DumpManifest manifest, TextWriter output)
    {
        output.WriteLine("Objects in the city");
        var keys = new SortedSet<string>(manifest.Counts.Keys, StringComparer.Ordinal);
        keys.UnionWith(manifest.FailureCounts.Keys);
        if (keys.Count == 0)
        {
            output.WriteLine($"{Indent}none recorded");
            return;
        }

        int keyWidth = keys.Max(key => key.Length) + 2;
        int valueWidth = keys.Max(key => Count(manifest.Counts, key).Length);
        foreach (string key in keys)
        {
            string failures = manifest.FailureCounts.TryGetValue(key, out long failed) && failed > 0
                ? string.Create(CultureInfo.InvariantCulture, $"  ({failed} skipped by the exporter)")
                : string.Empty;
            output.WriteLine($"{Indent}{key.PadRight(keyWidth)}{Count(manifest.Counts, key).PadLeft(valueWidth)}{failures}");
        }
    }

    private static void WriteSections(WorldLoadResult loaded, TextWriter output)
    {
        output.WriteLine("Sections");
        if (loaded.Sections.Count == 0)
        {
            output.WriteLine($"{Indent}none");
        }

        foreach (SectionLoadResult result in loaded.Sections)
        {
            string status = result.Status switch
            {
                SectionLoadStatus.Loaded => "loaded",
                SectionLoadStatus.Unknown => "skipped, unknown to this version",
                SectionLoadStatus.Incompatible => string.Create(CultureInfo.InvariantCulture, $"skipped, this version reads {result.LoaderVersion?.Major}.x"),
                _ => "skipped, damaged",
            };
            output.WriteLine($"{Indent}{result.Section.Name} {result.Section.Version}: {status}; {Contents(result)}");
            foreach (SectionStatistic statistic in result.Data?.Statistics ?? [])
            {
                output.WriteLine($"{Indent}{Indent}{statistic.Name}: {statistic.Value}");
            }
        }

        if (loaded.MissingSections.Count > 0)
        {
            output.WriteLine($"{Indent}Not in the dump: {string.Join(", ", loaded.MissingSections)}");
        }
    }

    private static void WriteAreaOfInterest(AreaOfInterest areaOfInterest, TextWriter output)
    {
        output.WriteLine("Area of interest");
        MapRect bounds = areaOfInterest.Bounds;
        WriteField(output, "Bounds", string.Create(CultureInfo.InvariantCulture, $"{bounds} ({bounds.Width:0.##} × {bounds.Depth:0.##} m)"));
        WriteField(output, "From", areaOfInterest.Source switch
        {
            AreaOfInterestSource.Tiles => "the tiles chosen with --tiles",
            AreaOfInterestSource.Content => string.Create(
                CultureInfo.InvariantCulture,
                $"the sections' content plus a {AreaOfInterestOptions.DefaultMarginMeters:0.##} m margin, within the map"),
            AreaOfInterestSource.UnlockedTiles => "the unlocked tiles; no section gives content bounds",
            _ => "the whole map; the dump has neither content bounds nor unlocked tiles",
        });
        if (areaOfInterest.ContentBounds is { } content)
        {
            WriteField(output, "Content", content.ToString());
        }
    }

    private static void WriteWarnings(DumpManifest manifest, IReadOnlyList<LoadMessage> messages, TextWriter output)
    {
        output.WriteLine("Warnings");
        if (manifest.Warnings.Count == 0 && messages.Count == 0)
        {
            output.WriteLine($"{Indent}none");
        }

        foreach (string warning in manifest.Warnings)
        {
            output.WriteLine($"{Indent}{"warning:",-9}from the exporter: {warning}");
        }

        foreach (LoadMessage message in messages)
        {
            string label = message.Severity switch
            {
                LoadMessageSeverity.Error => "error:",
                LoadMessageSeverity.Warning => "warning:",
                _ => "note:",
            };
            output.WriteLine($"{Indent}{label,-9}{message.Message}");
        }
    }

    private static string Contents(SectionLoadResult result)
    {
        int fileCount = result.Section.Files.Count;
        long bytes = result.Section.Files.Sum(file => file.Bytes);
        string files = string.Create(CultureInfo.InvariantCulture, $"{fileCount} {(fileCount == 1 ? "file" : "files")}, {DoctorReport.FormatSize(bytes)}");
        return result.Section.RecordCount is { } records
            ? string.Create(CultureInfo.InvariantCulture, $"{records} {(records == 1 ? "record" : "records")} in {files}")
            : files;
    }

    private static string Count(IDictionary<string, long> counts, string key) =>
        counts.TryGetValue(key, out long count) ? count.ToString(CultureInfo.InvariantCulture) : "?";

    private static void WriteField(TextWriter output, string label, string value) =>
        output.WriteLine($"{Indent}{(label + ":").PadRight(LabelWidth)}{value}");
}
