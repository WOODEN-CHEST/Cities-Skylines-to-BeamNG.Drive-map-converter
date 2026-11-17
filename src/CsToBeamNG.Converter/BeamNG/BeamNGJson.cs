using System;
using System.IO;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG;

/// <summary>How BeamNG JSON files are written.</summary>
/// <remarks>
/// <para>
/// UTF-8 without a byte order mark and <c>\n</c> line breaks. Only quotes, backslashes and control characters
/// are escaped: the game's own files contain non-ASCII text as is (docs/research/beamng-level-format.md).
/// </para>
/// <para>
/// Numbers are written by <see cref="Utf8JsonWriter"/>, which never depends on the current culture. Coordinates
/// are <see cref="float"/>s, as the game stores them, so they are written in the shortest form that reads back as
/// the same float, such as <c>115.728</c>; very small values use exponents (<c>1E-07</c>), as the game's own
/// files do.
/// </para>
/// </remarks>
internal static class BeamNGJson
{
    // Relaxed escaping keeps non-ASCII text readable; the files are read by the game, never embedded in HTML.
    private static readonly JavaScriptEncoder _encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>Options for a JSON object on one line, as in <c>items.level.json</c>.</summary>
    public static JsonWriterOptions LineOptions { get; } = new() { Encoder = _encoder, Indented = false };

    /// <summary>Options for an indented document, such as <c>info.json</c>.</summary>
    public static JsonWriterOptions DocumentOptions { get; } = new()
    {
        Encoder = _encoder,
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
    };

    /// <summary>Writes an indented document followed by a line break.</summary>
    public static void WriteDocument(Stream stream, JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        using (var writer = new Utf8JsonWriter(stream, DocumentOptions))
        {
            document.WriteTo(writer);
        }

        stream.WriteByte((byte)'\n');
    }

    /// <summary>A vector as an array of three numbers.</summary>
    public static JsonArray ToJson(Vector3 value) => Floats(value.X, value.Y, value.Z);

    /// <summary>A vector as an array of two numbers.</summary>
    public static JsonArray ToJson(Vector2 value) => Floats(value.X, value.Y);

    /// <summary>A vector as an array of four numbers.</summary>
    public static JsonArray ToJson(Vector4 value) => Floats(value.X, value.Y, value.Z, value.W);

    /// <summary>An array of numbers.</summary>
    public static JsonArray Floats(params ReadOnlySpan<float> values)
    {
        var array = new JsonArray();
        foreach (float value in values)
        {
            array.Add(ToJson(value));
        }

        return array;
    }

    /// <summary>A number. Negative zero becomes zero, so that sign noise from arithmetic doesn't reach the files.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite, which JSON can't represent.</exception>
    public static JsonValue ToJson(float value)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON can't represent NaN or infinity.");
        }

        return JsonValue.Create(value == 0 ? 0f : value);
    }
}
