#if NET
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// Reads the values that the prefab sections' JSON files share, as <see cref="PrefabJson"/> writes them. Only in the
/// .NET 10 build.
/// </summary>
internal static class PrefabJsonReader
{
    /// <summary>Reads the required object <c>class</c>.</summary>
    public static DumpItemClass ReadClass(JsonFieldReader json, JsonElement parent, string path)
    {
        JsonElement itemClass = json.RequiredObject(parent, "class", path);
        string classPath = JsonFieldReader.Child(path, "class");
        return new DumpItemClass
        {
            Name = json.RequiredString(itemClass, "name", classPath),
            Service = json.RequiredString(itemClass, "service", classPath),
            SubService = json.RequiredString(itemClass, "subService", classPath),
            Level = json.RequiredString(itemClass, "level", classPath),
            Layer = json.RequiredUInt32(itemClass, "layer", classPath),
        };
    }

    /// <summary>Reads a required vector.</summary>
    public static DumpVector3 ReadVector(JsonFieldReader json, JsonElement parent, string name, string path)
    {
        JsonElement vector = json.RequiredObject(parent, name, path);
        string vectorPath = JsonFieldReader.Child(path, name);
        return new DumpVector3(
            json.RequiredSingle(vector, "x", vectorPath),
            json.RequiredSingle(vector, "y", vectorPath),
            json.RequiredSingle(vector, "z", vectorPath));
    }

    /// <summary>Reads a required colour.</summary>
    public static DumpColor ReadColor(JsonFieldReader json, JsonElement parent, string name, string path) =>
        ReadColorValue(json, json.RequiredObject(parent, name, path), JsonFieldReader.Child(path, name));

    /// <summary>Reads a colour that is an array item.</summary>
    public static DumpColor ReadColorValue(JsonFieldReader json, JsonElement color, string path)
    {
        json.RequireObject(color, path);
        return new DumpColor(
            json.RequiredSingle(color, "r", path),
            json.RequiredSingle(color, "g", path),
            json.RequiredSingle(color, "b", path),
            json.RequiredSingle(color, "a", path));
    }

    /// <summary>Reads an array of exactly as many colours as <paramref name="colors"/> holds into it.</summary>
    public static void ReadColors(JsonFieldReader json, JsonElement parent, string name, string path, DumpColor[] colors)
    {
        var items = new List<(JsonElement Item, string Path)>(json.RequiredArray(parent, name, path));
        if (items.Count != colors.Length)
        {
            throw json.Error(
                JsonFieldReader.Child(path, name),
                string.Create(CultureInfo.InvariantCulture, $"expected {colors.Length} colours, found {items.Count}"));
        }

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = ReadColorValue(json, items[i].Item, items[i].Path);
        }
    }

    /// <summary>Reads an optional box; <see langword="null"/> if it is absent.</summary>
    public static DumpBounds? ReadOptionalBounds(JsonFieldReader json, JsonElement parent, string name, string path)
    {
        if (json.OptionalObject(parent, name, path) is not { } bounds)
        {
            return null;
        }

        string boundsPath = JsonFieldReader.Child(path, name);
        return new DumpBounds(ReadVector(json, bounds, "min", boundsPath), ReadVector(json, bounds, "max", boundsPath));
    }
}
#endif
