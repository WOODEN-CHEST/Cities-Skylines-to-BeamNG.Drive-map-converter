#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>Reads <c>manifest.json</c> (docs/dump-format.md, manifest.json). Only in the .NET 10 build.</summary>
/// <remarks>
/// It checks the format version first and rejects a different major version, whose schema may differ.
/// Then it checks every field and ignores unknown ones, which newer minor versions may add. Problems throw
/// a <see cref="DumpException"/> that names the file and the JSON path.
/// </remarks>
public static class ManifestReader
{
    // Writers write whole seconds; a fraction is accepted, so a later minor version may add precision.
    private static readonly string[] _timestampFormats = [ManifestWriter.TimestampFormat, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"];

    /// <summary>Reads a manifest file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="DumpException">The file doesn't exist, is damaged, or has an incompatible version.</exception>
    public static DumpManifest ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(JsonFieldReader.ReadFileBytes(path), path);
    }

    /// <summary>Parses a manifest from JSON text.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceName">The file's name or path, for messages.</param>
    /// <exception cref="DumpException">The manifest is damaged or has an incompatible version.</exception>
    public static DumpManifest Parse(string json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Parse(Encoding.UTF8.GetBytes(json), sourceName);
    }

    /// <summary>Parses a manifest from UTF-8 JSON.</summary>
    /// <param name="utf8Json">The JSON, encoded as UTF-8. A byte order mark is skipped.</param>
    /// <param name="sourceName">The file's name or path, for messages.</param>
    /// <exception cref="DumpException">The manifest is damaged or has an incompatible version.</exception>
    public static DumpManifest Parse(ReadOnlyMemory<byte> utf8Json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        using JsonDocument document = JsonFieldReader.Parse(utf8Json, sourceName);
        var reader = new JsonFieldReader(sourceName);
        JsonElement root = document.RootElement;
        reader.RequireObject(root, "$");

        DumpVersion version = reader.RequiredVersion(root, "formatVersion", "$");
        CheckVersion(version, sourceName);

        var manifest = new DumpManifest
        {
            FormatVersion = version,
            Complete = reader.RequiredBoolean(root, "complete", "$"),
            CreatedUtc = ReadTimestamp(reader, root),
            Exporter = ReadExporter(reader, root),
            Game = ReadGame(reader, root),
            City = ReadCity(reader, root),
        };

        ReadCounts(reader, root, "counts", manifest.Counts);
        ReadCounts(reader, root, "failureCounts", manifest.FailureCounts);
        foreach ((JsonElement item, string path) in reader.OptionalArray(root, "warnings", "$"))
        {
            manifest.Warnings.Add(reader.GetString(item, path));
        }

        ReadSections(reader, root, manifest.Sections);
        return manifest;
    }

    private static void CheckVersion(DumpVersion version, string sourceName)
    {
        DumpVersion current = DumpFormat.CurrentVersion;
        if (current.CanRead(version))
        {
            return;
        }

        string advice = version.Major > current.Major
            ? "A newer exporter wrote it; update CsToBeamNG to convert it."
            : "An older exporter wrote it; export the city again with the current one.";
        throw new DumpException(
            DumpErrorKind.Incompatible,
            string.Create(
                CultureInfo.InvariantCulture,
                $"'{sourceName}' has dump format version {version}, but this version of CsToBeamNG reads version {current.Major}.x. {advice}"));
    }

    private static DateTime ReadTimestamp(JsonFieldReader reader, JsonElement root)
    {
        string text = reader.RequiredString(root, "createdUtc", "$");
        if (!DateTime.TryParseExact(
            text,
            _timestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime value))
        {
            throw reader.Error("$.createdUtc", "expected a UTC time such as \"2026-11-17T09:30:00Z\", found \"" + text + "\"");
        }

        return value;
    }

    private static ExporterInfo ReadExporter(JsonFieldReader reader, JsonElement root)
    {
        JsonElement exporter = reader.RequiredObject(root, "exporter", "$");
        return new ExporterInfo(
            reader.RequiredString(exporter, "name", "$.exporter"),
            reader.RequiredString(exporter, "version", "$.exporter"));
    }

    private static GameInfo ReadGame(JsonFieldReader reader, JsonElement root)
    {
        JsonElement game = reader.RequiredObject(root, "game", "$");
        var info = new GameInfo { Version = reader.RequiredString(game, "version", "$.game") };
        foreach ((JsonElement mod, string path) in reader.OptionalArray(game, "mods", "$.game"))
        {
            reader.RequireObject(mod, path);
            info.Mods.Add(new ModInfo(reader.RequiredString(mod, "name", path), reader.OptionalString(mod, "workshopId", path)));
        }

        return info;
    }

    private static CityInfo ReadCity(JsonFieldReader reader, JsonElement root)
    {
        JsonElement city = reader.RequiredObject(root, "city", "$");
        var info = new CityInfo
        {
            Name = reader.RequiredString(city, "name", "$.city"),
            MapName = reader.OptionalString(city, "mapName", "$.city"),
            Environment = reader.OptionalString(city, "environment", "$.city"),
            MapTheme = reader.OptionalString(city, "mapTheme", "$.city"),
            LeftHandTraffic = reader.RequiredBoolean(city, "leftHandTraffic", "$.city"),
        };

        foreach ((JsonElement tile, string path) in reader.OptionalArray(city, "unlockedTiles", "$.city"))
        {
            reader.RequireObject(tile, path);
            info.UnlockedTiles.Add(new TileCoordinate(
                reader.RequiredInt32(tile, "x", path, 0, TileCoordinate.GridSize - 1),
                reader.RequiredInt32(tile, "z", path, 0, TileCoordinate.GridSize - 1)));
        }

        return info;
    }

    private static void ReadCounts(JsonFieldReader reader, JsonElement root, string name, IDictionary<string, long> counts)
    {
        foreach ((string key, JsonElement value, string path) in reader.OptionalObjectProperties(root, name, "$"))
        {
            counts[key] = reader.GetCount(value, path);
        }
    }

    private static void ReadSections(JsonFieldReader reader, JsonElement root, IList<SectionInfo> sections)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach ((JsonElement element, string path) in reader.RequiredArray(root, "sections", "$"))
        {
            reader.RequireObject(element, path);
            string name = reader.RequiredString(element, "name", path);
            if (!DumpNames.IsValidSectionName(name))
            {
                throw reader.Error(JsonFieldReader.Child(path, "name"), "'" + name + "' is not a valid section name: " + DumpNames.SectionNameRule);
            }

            if (!names.Add(name))
            {
                throw reader.Error(JsonFieldReader.Child(path, "name"), "the section name '" + name + "' is used twice");
            }

            var section = new SectionInfo
            {
                Name = name,
                Version = reader.RequiredVersion(element, "version", path),
                RecordCount = reader.OptionalCount(element, "recordCount", path),
            };

            var filePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach ((JsonElement file, string filePath) in reader.RequiredArray(element, "files", path))
            {
                reader.RequireObject(file, filePath);
                string relativePath = reader.RequiredString(file, "path", filePath);
                if (!DumpNames.IsValidFilePath(name, relativePath))
                {
                    throw reader.Error(JsonFieldReader.Child(filePath, "path"), "'" + relativePath + "' is not a valid file path: " + DumpNames.FilePathRule);
                }

                if (!filePaths.Add(relativePath))
                {
                    throw reader.Error(JsonFieldReader.Child(filePath, "path"), "the file '" + relativePath + "' is listed twice");
                }

                section.Files.Add(new SectionFileInfo(relativePath, reader.RequiredCount(file, "bytes", filePath)));
            }

            sections.Add(section);
        }
    }
}
#endif
