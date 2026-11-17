using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>Writes <c>manifest.json</c> (docs/dump-format.md, manifest.json).</summary>
/// <remarks>
/// The output is deterministic: properties in a fixed order, dictionaries sorted by key, lists in their
/// order. Optional strings are left out when they are null; collections are always written. An invalid
/// manifest, such as one without an exporter name or with an invalid section name, throws
/// <see cref="ArgumentException"/> before anything is written.
/// </remarks>
public static class ManifestWriter
{
    /// <summary>The format of <c>createdUtc</c>: UTC, to the second.</summary>
    internal const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    // Steam's published file ids are 64-bit numbers: at most 20 digits.
    private const int MaxWorkshopIdLength = 20;

    /// <summary>Writes a manifest as JSON.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="writer">Where the text goes. It should encode UTF-8 without a byte order mark.</param>
    /// <exception cref="ArgumentException">The manifest is invalid.</exception>
    public static void Write(DumpManifest manifest, TextWriter writer)
    {
        Guard.NotNull(writer, nameof(writer));
        Validate(manifest);
        using var json = new JsonWriter(writer, leaveOpen: true);
        WriteManifest(json, manifest);
    }

    /// <summary>Returns a manifest as JSON text.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <exception cref="ArgumentException">The manifest is invalid.</exception>
    public static string WriteToString(DumpManifest manifest)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Write(manifest, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Writes a manifest file, replacing an existing one. The text goes to <c>&lt;path&gt;.tmp</c> first,
    /// so a failure while writing doesn't leave a half-written manifest.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="path">The file, normally <c>&lt;dump folder&gt;/manifest.json</c>.</param>
    /// <exception cref="ArgumentException">The manifest is invalid.</exception>
    public static void WriteFile(DumpManifest manifest, string path)
    {
        Guard.NotNull(path, nameof(path));
        Validate(manifest);
        string temporaryPath = path + ".tmp";
        using (var writer = new StreamWriter(new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None), JsonWriter.Utf8))
        {
            Write(manifest, writer);
        }

        // .NET 3.5 can't move a file over an existing one.
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(temporaryPath, path);
    }

    private static void WriteManifest(JsonWriter json, DumpManifest manifest)
    {
        json.WriteStartObject();
        json.WriteString("formatVersion", manifest.FormatVersion.ToString());
        json.WriteBoolean("complete", manifest.Complete);
        json.WriteString("createdUtc", manifest.CreatedUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture));

        json.WriteStartObject("exporter");
        json.WriteString("name", manifest.Exporter.Name);
        json.WriteString("version", manifest.Exporter.Version);
        json.WriteEndObject();

        json.WriteStartObject("game");
        json.WriteString("version", manifest.Game.Version);
        json.WriteStartArray("mods");
        foreach (ModInfo mod in manifest.Game.Mods)
        {
            json.WriteStartObject();
            json.WriteString("name", mod.Name);
            WriteOptionalString(json, "workshopId", mod.WorkshopId);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();

        CityInfo city = manifest.City;
        json.WriteStartObject("city");
        json.WriteString("name", city.Name);
        WriteOptionalString(json, "mapName", city.MapName);
        WriteOptionalString(json, "environment", city.Environment);
        WriteOptionalString(json, "mapTheme", city.MapTheme);
        json.WriteBoolean("leftHandTraffic", city.LeftHandTraffic);
        json.WriteStartArray("unlockedTiles");
        foreach (TileCoordinate tile in city.UnlockedTiles)
        {
            json.WriteStartObject();
            json.WriteNumber("x", tile.X);
            json.WriteNumber("z", tile.Z);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();

        WriteCounts(json, "counts", manifest.Counts);
        WriteCounts(json, "failureCounts", manifest.FailureCounts);

        json.WriteStartArray("warnings");
        foreach (string warning in manifest.Warnings)
        {
            json.WriteStringValue(warning);
        }

        json.WriteEndArray();

        json.WriteStartArray("sections");
        foreach (SectionInfo section in manifest.Sections)
        {
            json.WriteStartObject();
            json.WriteString("name", section.Name);
            json.WriteString("version", section.Version.ToString());
            if (section.RecordCount.HasValue)
            {
                json.WriteNumber("recordCount", section.RecordCount.Value);
            }

            json.WriteStartArray("files");
            foreach (SectionFileInfo file in section.Files)
            {
                json.WriteStartObject();
                json.WriteString("path", file.Path);
                json.WriteNumber("bytes", file.Bytes);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteOptionalString(JsonWriter json, string propertyName, string? value)
    {
        if (value is not null)
        {
            json.WriteString(propertyName, value);
        }
    }

    // Sorted here as well, so the output doesn't depend on the dictionary's type.
    private static void WriteCounts(JsonWriter json, string propertyName, IDictionary<string, long> counts)
    {
        var keys = new List<string>(counts.Keys);
        keys.Sort(StringComparer.Ordinal);
        json.WriteStartObject(propertyName);
        foreach (string key in keys)
        {
            json.WriteNumber(key, counts[key]);
        }

        json.WriteEndObject();
    }

    private static void Validate(DumpManifest manifest)
    {
        Guard.NotNull(manifest, nameof(manifest));
        string? problem = FindProblem(manifest);
        if (problem is not null)
        {
            throw new ArgumentException("The manifest is invalid: " + problem + ".", nameof(manifest));
        }
    }

    private static string? FindProblem(DumpManifest manifest)
    {
        if (manifest.CreatedUtc.Kind != DateTimeKind.Utc)
        {
            return "createdUtc must be a UTC time (DateTimeKind.Utc)";
        }

        if (manifest.Exporter is null || string.IsNullOrEmpty(manifest.Exporter.Name))
        {
            return "exporter.name is required";
        }

        if (string.IsNullOrEmpty(manifest.Exporter.Version))
        {
            return "exporter.version is required";
        }

        if (manifest.Game is null || string.IsNullOrEmpty(manifest.Game.Version))
        {
            return "game.version is required";
        }

        if (manifest.City is null || manifest.City.Name is null)
        {
            return "city.name is required";
        }

        return FindModProblem(manifest.Game.Mods)
            ?? FindCountProblem(manifest.Counts, "counts")
            ?? FindCountProblem(manifest.FailureCounts, "failureCounts")
            ?? FindWarningProblem(manifest.Warnings)
            ?? FindSectionProblem(manifest.Sections);
    }

    private static string? FindModProblem(IList<ModInfo> mods)
    {
        for (int i = 0; i < mods.Count; i++)
        {
            ModInfo? mod = mods[i];
            if (mod is null || string.IsNullOrEmpty(mod.Name))
            {
                return Element("game.mods", i) + ".name is required";
            }

            if (mod.WorkshopId is not null && !IsWorkshopId(mod.WorkshopId))
            {
                return Element("game.mods", i) + ".workshopId must be decimal digits, not '" + mod.WorkshopId + "'";
            }
        }

        return null;
    }

    private static string? FindCountProblem(IDictionary<string, long> counts, string name)
    {
        foreach (KeyValuePair<string, long> count in counts)
        {
            if (!DumpNames.IsValidKey(count.Key))
            {
                return name + " has the invalid key '" + count.Key + "': " + DumpNames.KeyRule;
            }

            if (count.Value < 0)
            {
                return name + "." + count.Key + " is negative";
            }
        }

        return null;
    }

    private static string? FindWarningProblem(IList<string> warnings)
    {
        for (int i = 0; i < warnings.Count; i++)
        {
            if (warnings[i] is null)
            {
                return Element("warnings", i) + " is null";
            }
        }

        return null;
    }

    private static string? FindSectionProblem(IList<SectionInfo> sections)
    {
        var names = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < sections.Count; i++)
        {
            SectionInfo? section = sections[i];
            string location = Element("sections", i);
            if (section is null)
            {
                return location + " is null";
            }

            if (!DumpNames.IsValidSectionName(section.Name))
            {
                return location + ".name '" + section.Name + "' is invalid: " + DumpNames.SectionNameRule;
            }

            if (names.ContainsKey(section.Name))
            {
                return "the section name '" + section.Name + "' is used twice";
            }

            names.Add(section.Name, true);
            if (section.RecordCount < 0)
            {
                return location + ".recordCount is negative";
            }

            string? fileProblem = FindFileProblem(section, location);
            if (fileProblem is not null)
            {
                return fileProblem;
            }
        }

        return null;
    }

    private static string? FindFileProblem(SectionInfo section, string sectionLocation)
    {
        var paths = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < section.Files.Count; i++)
        {
            SectionFileInfo? file = section.Files[i];
            string location = sectionLocation + "." + Element("files", i);
            if (file is null)
            {
                return location + " is null";
            }

            if (!DumpNames.IsValidFilePath(section.Name, file.Path))
            {
                return location + ".path '" + file.Path + "' is invalid: " + DumpNames.FilePathRule;
            }

            if (paths.ContainsKey(file.Path))
            {
                return "section '" + section.Name + "' lists the file '" + file.Path + "' twice";
            }

            paths.Add(file.Path, true);
            if (file.Bytes < 0)
            {
                return location + ".bytes is negative";
            }
        }

        return null;
    }

    private static bool IsWorkshopId(string text)
    {
        if (text.Length == 0 || text.Length > MaxWorkshopIdLength)
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

    private static string Element(string array, int index) =>
        array + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
}
