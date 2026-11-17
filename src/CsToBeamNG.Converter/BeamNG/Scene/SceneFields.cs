using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// The class-specific fields of a scene object. They are written in ordinal order of their names, after the
/// fixed leading fields, as the game's editor writes them.
/// </summary>
public sealed class SceneFields
{
    // Written by the scene writer itself, before the class-specific fields.
    private static readonly HashSet<string> _reservedNames = new(StringComparer.Ordinal)
    {
        "name", "class", "persistentId", "__parent", "position",
    };

    private readonly SortedDictionary<string, JsonNode?> _fields = new(StringComparer.Ordinal);

    /// <summary>The number of fields.</summary>
    public int Count => _fields.Count;

    /// <summary>The fields in the order they are written.</summary>
    public IEnumerable<KeyValuePair<string, JsonNode?>> Entries => _fields;

    /// <summary>Sets a text field.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetNode(name, JsonValue.Create(value));
    }

    /// <summary>Sets a number field.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value; NaN and infinities are rejected.</param>
    public void Set(string name, float value) => SetNode(name, BeamNGJson.ToJson(value));

    /// <summary>Sets an integer field.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, int value) => SetNode(name, JsonValue.Create(value));

    /// <summary>Sets a <c>true</c>/<c>false</c> field.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, bool value) => SetNode(name, JsonValue.Create(value));

    /// <summary>Sets a field to an array of three numbers.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, Vector3 value) => SetNode(name, BeamNGJson.ToJson(value));

    /// <summary>Sets a field to any JSON value, such as an array of objects.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value; <see langword="null"/> writes <c>null</c>.</param>
    public void Set(string name, JsonNode? value) => SetNode(name, value);

    /// <summary>Copies all fields of <paramref name="other"/> into this set, replacing fields of the same name.</summary>
    /// <param name="other">The fields to copy.</param>
    public void SetAll(SceneFields other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (KeyValuePair<string, JsonNode?> field in other._fields)
        {
            _fields[field.Key] = field.Value;
        }
    }

    private void SetNode(string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_reservedNames.Contains(name))
        {
            throw new ArgumentException($"The scene writer sets '{name}' itself.", nameof(name));
        }

        _fields[name] = value;
    }
}
