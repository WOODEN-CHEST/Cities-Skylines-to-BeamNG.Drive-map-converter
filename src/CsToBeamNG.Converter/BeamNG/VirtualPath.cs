using System;
using System.Collections.Generic;
using System.IO;

namespace CsToBeamNG.Converter.BeamNG;

/// <summary>
/// A path in BeamNG.drive's virtual file system, such as <c>/levels/my_level/info.json</c>. It is always
/// absolute and separated by <c>/</c>, on every OS.
/// </summary>
/// <remarks>
/// <para>
/// The game mounts each mod folder at the root of its virtual file system, so <c>/levels/x/info.json</c> is the
/// file <c>levels/x/info.json</c> inside the mod folder (docs/research/beamng-level-format.md, Where levels
/// live). Level files refer to each other and to the game's own assets by these paths.
/// </para>
/// <para>
/// Build virtual paths only with this type, never with <see cref="Path.Combine(string, string)"/>, whose
/// separator depends on the OS. Segments are checked for structure only, because references to the game's
/// assets keep the game's spelling; names that we generate are checked by <see cref="BeamNGNames"/>.
/// </para>
/// </remarks>
public sealed class VirtualPath : IEquatable<VirtualPath>
{
    private const char Separator = '/';

    // Characters that Windows doesn't allow in file names; the game's paths never contain them.
    private static readonly char[] _forbiddenCharacters = ['\\', ':', '*', '?', '"', '<', '>', '|'];

    private readonly string[] _segments;

    private VirtualPath(string[] segments)
    {
        _segments = segments;
        Value = Separator + string.Join(Separator, segments);
    }

    /// <summary>The root, <c>/</c>.</summary>
    public static VirtualPath Root { get; } = new([]);

    /// <summary>The path as text, such as <c>/levels/x/info.json</c>.</summary>
    public string Value { get; }

    /// <summary>The segments between the separators, such as <c>levels</c>, <c>x</c> and <c>info.json</c>.</summary>
    public IReadOnlyList<string> Segments => _segments;

    /// <summary>The last segment, or an empty string for the root.</summary>
    public string Name => _segments.Length == 0 ? string.Empty : _segments[^1];

    /// <summary>The folder that contains this path, or <see langword="null"/> for the root.</summary>
    public VirtualPath? Parent => _segments.Length == 0 ? null : new VirtualPath(_segments[..^1]);

    /// <summary>Parses an absolute virtual path such as <c>/assets/materials/x.png</c>.</summary>
    /// <param name="value">The path. It starts with <c>/</c> and has no empty, <c>.</c> or <c>..</c> segments.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentException">The path is malformed.</exception>
    public static VirtualPath Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || value[0] != Separator)
        {
            throw new ArgumentException($"A virtual path must start with '/': '{value}'.", nameof(value));
        }

        return value.Length == 1 ? Root : Root.Combine(value[1..]);
    }

    /// <summary>The folder of a level, <c>/levels/&lt;name&gt;</c>.</summary>
    /// <param name="levelName">The level's folder name; see <see cref="BeamNGNames.IsGeneratedName"/>.</param>
    /// <returns>The level folder.</returns>
    /// <exception cref="ArgumentException">The name isn't a valid generated name.</exception>
    public static VirtualPath ForLevel(string levelName)
    {
        BeamNGNames.ThrowIfNotGeneratedName(levelName, nameof(levelName));
        return Root.Combine("levels").Combine(levelName);
    }

    /// <summary>Appends a relative path such as <c>art/terrains/main.materials.json</c>.</summary>
    /// <param name="relativePath">One or more segments separated by <c>/</c>.</param>
    /// <returns>The combined path.</returns>
    /// <exception cref="ArgumentException">The relative path is empty or malformed.</exception>
    public VirtualPath Combine(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string[] added = relativePath.Split(Separator);
        foreach (string segment in added)
        {
            ValidateSegment(segment, relativePath);
        }

        return new VirtualPath([.. _segments, .. added]);
    }

    /// <summary>Whether this path is <paramref name="folder"/> or lies below it.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns><see langword="true"/> if every segment of <paramref name="folder"/> starts this path.</returns>
    public bool IsWithin(VirtualPath folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (folder._segments.Length > _segments.Length)
        {
            return false;
        }

        for (int i = 0; i < folder._segments.Length; i++)
        {
            if (!string.Equals(folder._segments[i], _segments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The path relative to <paramref name="folder"/>, separated by <c>/</c>.</summary>
    /// <param name="folder">A folder that contains this path.</param>
    /// <returns>The relative path, such as <c>art/x.dae</c>.</returns>
    /// <exception cref="ArgumentException">This path doesn't lie below <paramref name="folder"/>.</exception>
    public string RelativeTo(VirtualPath folder)
    {
        if (!IsWithin(folder) || folder._segments.Length == _segments.Length)
        {
            throw new ArgumentException($"'{Value}' doesn't lie below '{folder.Value}'.", nameof(folder));
        }

        return string.Join(Separator, _segments, folder._segments.Length, _segments.Length - folder._segments.Length);
    }

    /// <summary>
    /// The file-system path of this virtual path inside a folder that is mounted at the virtual root, such as a
    /// mod folder.
    /// </summary>
    /// <param name="mountedFolder">The folder that the virtual root corresponds to.</param>
    /// <returns>The path, with the OS's separators.</returns>
    public string ToFileSystemPath(string mountedFolder)
    {
        ArgumentNullException.ThrowIfNull(mountedFolder);
        return Path.Join([mountedFolder, .. _segments]);
    }

    /// <inheritdoc/>
    public bool Equals(VirtualPath? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as VirtualPath);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>The path as text, such as <c>/levels/x/info.json</c>.</summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;

    private static void ValidateSegment(string segment, string relativePath)
    {
        if (segment.Length == 0 || segment is "." or "..")
        {
            throw new ArgumentException($"Empty, '.' or '..' segment in '{relativePath}'.", nameof(relativePath));
        }

        foreach (char c in segment)
        {
            if (c < ' ' || Array.IndexOf(_forbiddenCharacters, c) >= 0)
            {
                throw new ArgumentException(
                    $"Character U+{(int)c:X4} isn't allowed in a virtual path: '{relativePath}'.", nameof(relativePath));
            }
        }
    }
}
