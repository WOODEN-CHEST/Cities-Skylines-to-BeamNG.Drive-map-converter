using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsToBeamNG.Dump;

/// <summary>
/// Where the exporter puts dumps and how it names their folders: <c>&lt;CS1 user data&gt;/CsToBeamNG/dumps/</c>,
/// with one folder per export named <c>&lt;city&gt;_&lt;yyyyMMdd-HHmmss&gt;</c> (docs/dump-format.md, Layout).
/// The format itself doesn't depend on the folder's name or location.
/// </summary>
public static class DumpLocation
{
    /// <summary>The folder in the game's user-data folder that holds everything this project writes there.</summary>
    public const string ProductFolderName = "CsToBeamNG";

    /// <summary>The folder in <see cref="ProductFolderName"/> that holds the dumps.</summary>
    public const string DumpsFolderName = "dumps";

    /// <summary>
    /// The most characters a folder name takes from the city's name. This keeps the dump's paths within
    /// Windows' 260-character limit, which .NET 3.5 enforces (docs/dump-format.md, Names).
    /// </summary>
    public const int MaxCityPartLength = 32;

    /// <summary>The city part of a folder name when the city's name has no usable characters.</summary>
    public const string FallbackCityPart = "city";

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>Returns the dumps folder for a Cities: Skylines user-data folder.</summary>
    /// <param name="userDataFolder">The game's user-data folder, such as <c>~/.local/share/Colossal Order/Cities_Skylines</c>.</param>
    public static string GetDumpsFolder(string userDataFolder)
    {
        Guard.NotNull(userDataFolder, nameof(userDataFolder));
        return Path.Combine(Path.Combine(userDataFolder, ProductFolderName), DumpsFolderName);
    }

    /// <summary>
    /// Returns the folder name for an export: the city's name in lowercase ASCII letters, digits and
    /// hyphens, at most <see cref="MaxCityPartLength"/> characters, then <c>_</c> and the time as
    /// <c>yyyyMMdd-HHmmss</c>, such as <c>new-york_20261117-153000</c>.
    /// </summary>
    /// <param name="cityName">The city's name, which may be empty or <see langword="null"/>.</param>
    /// <param name="time">When the export started, normally in local time, which the user recognises.</param>
    public static string GetFolderName(string? cityName, DateTime time) =>
        ToCityPart(cityName) + "_" + time.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the path of a dump folder that doesn't exist yet: <see cref="GetFolderName"/> in the dumps
    /// folder, with <c>-2</c>, <c>-3</c> and so on appended if that is taken, such as by two exports in
    /// the same second. Nothing is created.
    /// </summary>
    /// <param name="dumpsFolder">The dumps folder (<see cref="GetDumpsFolder"/>).</param>
    /// <param name="cityName">The city's name, which may be empty or <see langword="null"/>.</param>
    /// <param name="time">When the export started, normally in local time.</param>
    public static string ChooseNewFolder(string dumpsFolder, string? cityName, DateTime time)
    {
        Guard.NotNull(dumpsFolder, nameof(dumpsFolder));
        string name = GetFolderName(cityName, time);
        string path = Path.Combine(dumpsFolder, name);
        for (int suffix = 2; Directory.Exists(path) || File.Exists(path); suffix++)
        {
            path = Path.Combine(dumpsFolder, name + "-" + suffix.ToString(CultureInfo.InvariantCulture));
        }

        return path;
    }

    // Keeps ASCII letters (lowercased) and digits; every run of other characters becomes one hyphen.
    // Letters outside ASCII are dropped rather than transliterated: the name only helps the user find
    // the folder, and the manifest holds the real one.
    private static string ToCityPart(string? cityName)
    {
        var part = new StringBuilder(MaxCityPartLength);
        bool pendingHyphen = false;
        foreach (char c in cityName ?? string.Empty)
        {
            char kept = c switch
            {
                >= 'a' and <= 'z' or >= '0' and <= '9' => c,
                >= 'A' and <= 'Z' => (char)(c - 'A' + 'a'),
                _ => '\0',
            };
            if (kept == '\0')
            {
                pendingHyphen = part.Length > 0;
                continue;
            }

            if (pendingHyphen)
            {
                if (part.Length + 2 > MaxCityPartLength)
                {
                    break;
                }

                part.Append('-');
                pendingHyphen = false;
            }

            if (part.Length == MaxCityPartLength)
            {
                break;
            }

            part.Append(kept);
        }

        return part.Length == 0 ? FallbackCityPart : part.ToString();
    }
}
