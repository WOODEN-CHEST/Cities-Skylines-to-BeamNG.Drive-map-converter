using CsToBeamNG.Dump.Json;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// Writes the values that the prefab sections' JSON files share: item classes, vectors, colours and boxes
/// (docs/dump-format.md, Vectors and curves), and checks them before they are written.
/// </summary>
internal static class PrefabJson
{
    /// <summary>Writes the object <c>class</c>.</summary>
    public static void WriteClass(JsonWriter json, DumpItemClass itemClass)
    {
        json.WriteStartObject("class");
        json.WriteString("name", itemClass.Name);
        json.WriteString("service", itemClass.Service);
        json.WriteString("subService", itemClass.SubService);
        json.WriteString("level", itemClass.Level);
        json.WriteNumber("layer", itemClass.Layer);
        json.WriteEndObject();
    }

    /// <summary>Writes a vector as an object with the numbers <c>x</c>, <c>y</c> and <c>z</c>.</summary>
    public static void WriteVector(JsonWriter json, string name, DumpVector3 vector)
    {
        json.WriteStartObject(name);
        json.WriteNumber("x", vector.X);
        json.WriteNumber("y", vector.Y);
        json.WriteNumber("z", vector.Z);
        json.WriteEndObject();
    }

    /// <summary>Writes a colour as an object with the numbers <c>r</c>, <c>g</c>, <c>b</c> and <c>a</c>.</summary>
    public static void WriteColor(JsonWriter json, string name, DumpColor color)
    {
        json.WritePropertyName(name);
        WriteColorValue(json, color);
    }

    /// <summary>Writes a colour as an array item.</summary>
    public static void WriteColorValue(JsonWriter json, DumpColor color)
    {
        json.WriteStartObject();
        json.WriteNumber("r", color.R);
        json.WriteNumber("g", color.G);
        json.WriteNumber("b", color.B);
        json.WriteNumber("a", color.A);
        json.WriteEndObject();
    }

    /// <summary>Writes a box as an object with the vectors <c>min</c> and <c>max</c>; nothing if it is absent.</summary>
    public static void WriteOptionalBounds(JsonWriter json, string name, DumpBounds? bounds)
    {
        if (bounds is not { } box)
        {
            return;
        }

        json.WriteStartObject(name);
        WriteVector(json, "min", box.Min);
        WriteVector(json, "max", box.Max);
        json.WriteEndObject();
    }

    /// <summary>Returns why a prefab's name or class can't be written, or <see langword="null"/> if they can.</summary>
    public static string? CheckNameAndClass(string? name, DumpItemClass? itemClass)
    {
        if (name is null)
        {
            return "its name is missing";
        }

        if (itemClass is null || itemClass.Name is null || itemClass.Service is null || itemClass.SubService is null || itemClass.Level is null)
        {
            return "its class or the class's name, service, sub-service or level is missing";
        }

        return null;
    }

    /// <summary>Whether a box is absent or finite.</summary>
    public static bool IsFiniteOrAbsent(DumpBounds? bounds) => bounds is not { } box || box.IsFinite;

    /// <summary>Whether a number is finite.</summary>
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
