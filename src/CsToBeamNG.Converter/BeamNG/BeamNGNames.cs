using System;

namespace CsToBeamNG.Converter.BeamNG;

/// <summary>Rules for the names we generate in BeamNG output.</summary>
public static class BeamNGNames
{
    /// <summary>The prefix of every mod folder that this tool creates or replaces.</summary>
    /// <remarks>Other folders in <c>mods/unpacked</c> belong to the user and are never touched (agents/AGENTS.md).</remarks>
    public const string ModFolderPrefix = "cstobeamng_";

    /// <summary>The longest generated name we allow, which keeps paths short enough for Windows.</summary>
    public const int MaxGeneratedNameLength = 64;

    /// <summary>
    /// Whether <paramref name="name"/> is valid for a file, folder or level name that we generate: lowercase ASCII
    /// letters, digits, <c>_</c>, <c>-</c> and <c>.</c>, not starting or ending with <c>.</c>, at most
    /// <see cref="MaxGeneratedNameLength"/> characters.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> if the name is valid.</returns>
    /// <remarks>
    /// Lowercase only, because Linux file systems are case-sensitive and references must match exactly
    /// (agents/conventions.md, Cross-platform rules).
    /// </remarks>
    public static bool IsGeneratedName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxGeneratedNameLength || name[0] == '.' || name[^1] == '.')
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is valid for a scene object or group: ASCII letters, digits and <c>_</c>,
    /// not starting with a digit.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> if the name is valid.</returns>
    /// <remarks>
    /// The game looks objects up by name and loads each group from a folder of the same name, so names follow
    /// the identifier rules of its script engine. They keep the case the game expects, such as <c>MissionGroup</c>.
    /// </remarks>
    public static bool IsSceneName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxGeneratedNameLength || name[0] is >= '0' and <= '9')
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether <paramref name="name"/> is a valid generated name that starts with <see cref="ModFolderPrefix"/>.</summary>
    /// <param name="name">The mod folder name.</param>
    /// <returns><see langword="true"/> if the name is valid.</returns>
    public static bool IsOwnModFolderName(string? name) =>
        IsGeneratedName(name) && name!.StartsWith(ModFolderPrefix, StringComparison.Ordinal) && name.Length > ModFolderPrefix.Length;

    internal static void ThrowIfNotGeneratedName(string? name, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);
        if (!IsGeneratedName(name))
        {
            throw new ArgumentException(
                $"'{name}' isn't a valid generated name: use lowercase a-z, 0-9, '_', '-' and '.', at most {MaxGeneratedNameLength} characters.",
                parameterName);
        }
    }

    internal static void ThrowIfNotSceneName(string? name, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);
        if (!IsSceneName(name))
        {
            throw new ArgumentException(
                $"'{name}' isn't a valid scene object name: use ASCII letters, digits and '_', not starting with a digit.",
                parameterName);
        }
    }
}
