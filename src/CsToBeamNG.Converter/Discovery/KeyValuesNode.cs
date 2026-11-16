using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// One entry of a Valve KeyValues ("VDF") document: a key with either a string value or child
/// entries. Steam's library list and app manifests use this format.
/// </summary>
internal sealed class KeyValuesNode
{
    public KeyValuesNode(string key, string value)
    {
        Key = key;
        Value = value;
        Children = [];
    }

    public KeyValuesNode(string key, IReadOnlyList<KeyValuesNode> children)
    {
        Key = key;
        Children = children;
    }

    public string Key { get; }

    /// <summary>The value, or <see langword="null"/> for a section, which has child entries instead.</summary>
    public string? Value { get; }

    /// <summary>The child entries in file order. Keys may repeat.</summary>
    public IReadOnlyList<KeyValuesNode> Children { get; }

    /// <summary>
    /// Returns the first child with the key. Keys are compared case-insensitively, like Steam does:
    /// it has written both "LibraryFolders" and "libraryfolders".
    /// </summary>
    public KeyValuesNode? Find(string key)
    {
        foreach (KeyValuesNode child in Children)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>Returns the value of the first child with the key, or <see langword="null"/>.</summary>
    public string? FindValue(string key) => Find(key)?.Value;
}
