using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump;

/// <summary>
/// Collects the strings that a section's records refer to, such as prefab names, and assigns their
/// indices (docs/dump-format.md, String tables).
/// </summary>
/// <remarks>
/// Index 0 is always the empty string and means "no string". Each other string is stored once, in the
/// order it was first added, so the indices are deterministic. Write the table with
/// <see cref="SectionWriter.WriteStringTable"/>.
/// </remarks>
public sealed class StringTableBuilder
{
    private readonly List<string> _strings = [string.Empty];
    private readonly Dictionary<string, uint> _indices = new(StringComparer.Ordinal) { [string.Empty] = 0 };

    /// <summary>The number of entries, including the empty string at index 0.</summary>
    public int Count => _strings.Count;

    /// <summary>Returns the index of a string, adding it if it is new.</summary>
    /// <param name="value">The string. <see langword="null"/> and the empty string have index 0.</param>
    /// <returns>The index to store in a <c>str</c> field.</returns>
    public uint Add(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        if (!_indices.TryGetValue(value!, out uint index))
        {
            index = (uint)_strings.Count;
            _strings.Add(value!);
            _indices.Add(value!, index);
        }

        return index;
    }

    /// <summary>Writes the table as a JSON object: <c>{ "strings": [ ... ] }</c>.</summary>
    /// <param name="writer">The writer, at a position where a value can be written.</param>
    public void WriteTo(JsonWriter writer)
    {
        Guard.NotNull(writer, nameof(writer));
        writer.WriteStartObject();
        writer.WriteStartArray("strings");
        foreach (string value in _strings)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
