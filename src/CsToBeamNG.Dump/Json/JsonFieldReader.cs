#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CsToBeamNG.Dump.Json;

/// <summary>
/// Reads typed values from a parsed JSON document, following the dump's JSON conventions
/// (docs/dump-format.md, JSON files). Problems throw a <see cref="DumpException"/> that names the file and
/// the JSON path, such as <c>$.sections[2].version</c>.
/// </summary>
/// <remarks>
/// Optional properties may be missing or <c>null</c>. Integers must be written without a fraction or an
/// exponent. Unknown properties are ignored, as minor versions may add them.
/// </remarks>
internal sealed class JsonFieldReader
{
    private const int MaxQuotedLength = 40;

    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowDuplicateProperties = false,
    };

    public JsonFieldReader(string sourceName) => SourceName = sourceName;

    /// <summary>The file's path, for messages.</summary>
    public string SourceName { get; }

    /// <summary>Reads a file's bytes. A missing file throws a <see cref="DumpException"/> (not found).</summary>
    public static byte[] ReadFileBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new DumpException(DumpErrorKind.NotFound, string.Create(CultureInfo.InvariantCulture, $"'{path}' doesn't exist."), e);
        }
    }

    /// <summary>Parses UTF-8 JSON, skipping a byte order mark. Invalid JSON throws a <see cref="DumpException"/> (corrupt).</summary>
    public static JsonDocument Parse(ReadOnlyMemory<byte> utf8Json, string sourceName)
    {
        ReadOnlySpan<byte> byteOrderMark = [0xEF, 0xBB, 0xBF];
        if (utf8Json.Span.StartsWith(byteOrderMark))
        {
            utf8Json = utf8Json[byteOrderMark.Length..];
        }

        try
        {
            return JsonDocument.Parse(utf8Json, _documentOptions);
        }
        catch (JsonException e)
        {
            throw new DumpException(
                DumpErrorKind.Corrupt,
                string.Create(CultureInfo.InvariantCulture, $"'{sourceName}' is not valid JSON: {e.Message}"),
                e);
        }
    }

    public static string Child(string path, string name) => path + "." + name;

    public static string Item(string path, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");

    public DumpException Error(string path, string problem) =>
        new(DumpErrorKind.Corrupt, string.Create(CultureInfo.InvariantCulture, $"'{SourceName}' is invalid at {path}: {problem}."));

    public void RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Error(path, "expected an object, found " + Describe(element));
        }
    }

    public JsonElement RequiredObject(JsonElement parent, string name, string parentPath)
    {
        JsonElement value = Required(parent, name, parentPath);
        RequireObject(value, Child(parentPath, name));
        return value;
    }

    public string RequiredString(JsonElement parent, string name, string parentPath) =>
        GetString(Required(parent, name, parentPath), Child(parentPath, name));

    public string? OptionalString(JsonElement parent, string name, string parentPath) =>
        TryGetOptional(parent, name, out JsonElement value) ? GetString(value, Child(parentPath, name)) : null;

    public bool RequiredBoolean(JsonElement parent, string name, string parentPath)
    {
        JsonElement value = Required(parent, name, parentPath);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Error(Child(parentPath, name), "expected true or false, found " + Describe(value)),
        };
    }

    /// <summary>A whole number of 0 or more.</summary>
    public long RequiredCount(JsonElement parent, string name, string parentPath) =>
        GetCount(Required(parent, name, parentPath), Child(parentPath, name));

    /// <summary>A whole number of 0 or more, or <see langword="null"/> if absent.</summary>
    public long? OptionalCount(JsonElement parent, string name, string parentPath) =>
        TryGetOptional(parent, name, out JsonElement value) ? GetCount(value, Child(parentPath, name)) : null;

    public long GetCount(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out long value) || value < 0)
        {
            throw Error(path, "expected a whole number of 0 or more, found " + Describe(element));
        }

        return value;
    }

    public int RequiredInt32(JsonElement parent, string name, string parentPath, int minimum, int maximum)
    {
        JsonElement element = Required(parent, name, parentPath);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value) || value < minimum || value > maximum)
        {
            throw Error(
                Child(parentPath, name),
                string.Create(CultureInfo.InvariantCulture, $"expected a whole number from {minimum} to {maximum}, found {Describe(element)}"));
        }

        return value;
    }

    /// <summary>A whole number from 0 to <see cref="uint.MaxValue"/>, such as an id or the raw value of a set of flags.</summary>
    public uint RequiredUInt32(JsonElement parent, string name, string parentPath)
    {
        JsonElement element = Required(parent, name, parentPath);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint value))
        {
            throw Error(
                Child(parentPath, name),
                string.Create(CultureInfo.InvariantCulture, $"expected a whole number from 0 to {uint.MaxValue}, found {Describe(element)}"));
        }

        return value;
    }

    /// <summary>A finite number, read as a 32-bit float (docs/dump-format.md, JSON files).</summary>
    public float RequiredSingle(JsonElement parent, string name, string parentPath)
    {
        JsonElement element = Required(parent, name, parentPath);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetSingle(out float value) || !float.IsFinite(value))
        {
            throw Error(Child(parentPath, name), "expected a finite number, found " + Describe(element));
        }

        return value;
    }

    public DumpVersion RequiredVersion(JsonElement parent, string name, string parentPath)
    {
        JsonElement element = Required(parent, name, parentPath);
        if (element.ValueKind != JsonValueKind.String || !DumpVersion.TryParse(element.GetString(), out DumpVersion version))
        {
            throw Error(Child(parentPath, name), "expected a version of the form major.minor, such as \"0.1\", found " + Describe(element));
        }

        return version;
    }

    /// <summary>The items of a required array, each with its path.</summary>
    public IEnumerable<(JsonElement Item, string Path)> RequiredArray(JsonElement parent, string name, string parentPath) =>
        GetArray(Required(parent, name, parentPath), Child(parentPath, name));

    /// <summary>The items of an optional array, each with its path; none if it is absent.</summary>
    public IEnumerable<(JsonElement Item, string Path)> OptionalArray(JsonElement parent, string name, string parentPath) =>
        TryGetOptional(parent, name, out JsonElement value) ? GetArray(value, Child(parentPath, name)) : [];

    /// <summary>The properties of an optional object, each with its path; none if it is absent.</summary>
    public IEnumerable<(string Name, JsonElement Value, string Path)> OptionalObjectProperties(JsonElement parent, string name, string parentPath)
    {
        if (!TryGetOptional(parent, name, out JsonElement value))
        {
            return [];
        }

        string path = Child(parentPath, name);
        RequireObject(value, path);
        var properties = new List<(string, JsonElement, string)>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            properties.Add((property.Name, property.Value, Child(path, property.Name)));
        }

        return properties;
    }

    public string GetString(JsonElement element, string path) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw Error(path, "expected a string, found " + Describe(element));

    private static bool TryGetOptional(JsonElement parent, string name, out JsonElement value) =>
        parent.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    private static string Describe(JsonElement element)
    {
        string kind = element.ValueKind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "an array",
            JsonValueKind.String => "the string",
            JsonValueKind.Number => "the number",
            JsonValueKind.True or JsonValueKind.False => "the value",
            JsonValueKind.Null => "null",
            _ => "nothing",
        };

        if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return kind;
        }

        string text = element.GetRawText();
        return kind + " " + (text.Length <= MaxQuotedLength ? text : text[..MaxQuotedLength] + "...");
    }

    private JsonElement Required(JsonElement parent, string name, string parentPath)
    {
        if (!TryGetOptional(parent, name, out JsonElement value))
        {
            throw Error(Child(parentPath, name), "this required property is missing");
        }

        return value;
    }

    private List<(JsonElement, string)> GetArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Error(path, "expected an array, found " + Describe(element));
        }

        var items = new List<(JsonElement, string)>();
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            items.Add((item, Item(path, index)));
            index++;
        }

        return items;
    }
}
#endif
