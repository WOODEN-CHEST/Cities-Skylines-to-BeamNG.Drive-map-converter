using System;
using System.Text;
using CsToBeamNG.Converter.BeamNG;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>
/// Names of converted levels. A level and its mod folder share one name, <c>cstobeamng_&lt;name&gt;</c>, made of
/// lowercase ASCII letters, digits and underscores, so it is valid as a folder, in virtual paths and inside scene names.
/// </summary>
public static class LevelNames
{
    /// <summary>The name used when a city's name has no usable characters.</summary>
    public const string Fallback = "city";

    /// <summary>
    /// Returns the level name for a name the user gave: <c>cstobeamng_</c> plus the name, unless it already starts with
    /// that prefix.
    /// </summary>
    /// <param name="name">The name: lowercase letters a–z, digits and underscores.</param>
    /// <returns>The level name.</returns>
    /// <exception cref="ArgumentException">The name has other characters, or the level name would be too long.</exception>
    public static string FromUserName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string levelName = name.StartsWith(BeamNGNames.ModFolderPrefix, StringComparison.Ordinal) ? name : BeamNGNames.ModFolderPrefix + name;
        if (!IsLevelName(levelName))
        {
            throw new ArgumentException(
                $"'{name}' can't name a level: use lowercase letters a-z, digits and '_', at most "
                + $"{BeamNGNames.MaxGeneratedNameLength - BeamNGNames.ModFolderPrefix.Length} characters.",
                nameof(name));
        }

        return levelName;
    }

    /// <summary>
    /// Returns a level name made from a city's name: lowercase ASCII letters and digits, with underscores for the rest,
    /// such as <c>cstobeamng_san_palma</c> for "San Palma".
    /// </summary>
    /// <param name="cityName">The city's name, or <see langword="null"/>.</param>
    /// <returns>The level name.</returns>
    public static string FromCityName(string? cityName)
    {
        var builder = new StringBuilder();
        int maxLength = BeamNGNames.MaxGeneratedNameLength - BeamNGNames.ModFolderPrefix.Length;
        foreach (char c in (cityName ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            char lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(lower);
            }
            else if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark
                && builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        string name = builder.ToString().Trim('_');
        return BeamNGNames.ModFolderPrefix + (name.Length == 0 ? Fallback : name);
    }

    /// <summary>Whether a name is a valid level name: the prefix, then lowercase letters, digits and underscores.</summary>
    /// <param name="levelName">The name.</param>
    public static bool IsLevelName(string? levelName)
    {
        if (!BeamNGNames.IsOwnModFolderName(levelName))
        {
            return false;
        }

        foreach (char c in levelName!)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
