#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump;

/// <summary>
/// A section's string table, read from its JSON file (docs/dump-format.md, String tables). Only in the
/// .NET 10 build; <see cref="StringTableBuilder"/> writes them.
/// </summary>
public sealed class StringTable
{
    private readonly string[] _strings;

    private StringTable(string[] strings, string sourceName)
    {
        _strings = strings;
        SourceName = sourceName;
    }

    /// <summary>The file's path, for messages.</summary>
    public string SourceName { get; }

    /// <summary>The number of entries, including the empty string at index 0.</summary>
    public int Count => _strings.Length;

    /// <summary>The entries. Index 0 is the empty string, which means "no string".</summary>
    public IReadOnlyList<string> Strings => _strings;

    /// <summary>Reads a string table file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="DumpException">The file doesn't exist or is damaged.</exception>
    public static StringTable ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(JsonFieldReader.ReadFileBytes(path), path);
    }

    /// <summary>Parses a string table from UTF-8 JSON.</summary>
    /// <param name="utf8Json">The JSON, encoded as UTF-8. A byte order mark is skipped.</param>
    /// <param name="sourceName">The file's name or path, for messages.</param>
    /// <exception cref="DumpException">The table is damaged.</exception>
    public static StringTable Parse(ReadOnlyMemory<byte> utf8Json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        using JsonDocument document = JsonFieldReader.Parse(utf8Json, sourceName);
        var reader = new JsonFieldReader(sourceName);
        reader.RequireObject(document.RootElement, "$");
        var strings = new List<string>();
        foreach ((JsonElement item, string path) in reader.RequiredArray(document.RootElement, "strings", "$"))
        {
            strings.Add(reader.GetString(item, path));
        }

        if (strings.Count == 0 || strings[0].Length != 0)
        {
            throw reader.Error("$.strings[0]", "the first entry must be the empty string");
        }

        return new StringTable([.. strings], sourceName);
    }

    /// <summary>Gets an entry.</summary>
    /// <param name="index">The index from a <c>str</c> field.</param>
    /// <param name="value">The string; empty for index 0.</param>
    /// <returns>Whether the index is within the table.</returns>
    public bool TryGet(uint index, [NotNullWhen(true)] out string? value)
    {
        if (index < (uint)_strings.Length)
        {
            value = _strings[index];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Gets an entry.</summary>
    /// <param name="index">The index from a <c>str</c> field.</param>
    /// <returns>The string; empty for index 0.</returns>
    /// <exception cref="DumpException">The index is beyond the table: the data that refers to it is damaged.</exception>
    public string Get(uint index) =>
        TryGet(index, out string? value)
            ? value
            : throw new DumpException(
                DumpErrorKind.Corrupt,
                string.Create(CultureInfo.InvariantCulture, $"'{SourceName}' has {Count} strings, but index {index} was requested."));
}
#endif
