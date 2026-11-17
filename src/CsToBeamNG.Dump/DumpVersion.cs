using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CsToBeamNG.Dump;

/// <summary>
/// A version of the dump format or of a section, written as <c>major.minor</c>, for example <c>0.1</c>.
/// </summary>
/// <remarks>
/// A new major version breaks readers; a new minor version only adds optional data. A reader therefore
/// reads data whose major version equals its own, whatever the minor version (docs/dump-format.md,
/// Versioning).
/// </remarks>
public readonly struct DumpVersion : IEquatable<DumpVersion>, IComparable<DumpVersion>
{
    // Nine digits always fit into an int. Real versions have one or two.
    private const int MaxDigits = 9;

    /// <summary>Creates a version.</summary>
    /// <param name="major">The major version, 0 or more.</param>
    /// <param name="minor">The minor version, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">A number is negative.</exception>
    public DumpVersion(int major, int minor)
    {
        if (major < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), major, "Version numbers can't be negative.");
        }

        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), minor, "Version numbers can't be negative.");
        }

        Major = major;
        Minor = minor;
    }

    /// <summary>The major version. Data with another major version can't be read.</summary>
    public int Major { get; }

    /// <summary>The minor version. Newer minor versions only add optional data.</summary>
    public int Minor { get; }

    /// <summary>Whether a reader of this version reads data of <paramref name="dataVersion"/>: the major versions are equal.</summary>
    /// <param name="dataVersion">The version the data was written with.</param>
    public bool CanRead(DumpVersion dataVersion) => Major == dataVersion.Major;

    /// <summary>Parses a version of the form <c>major.minor</c>.</summary>
    /// <param name="text">Two decimal numbers without leading zeros, separated by a dot, such as <c>0.1</c>.</param>
    /// <exception cref="FormatException"><paramref name="text"/> is not a version.</exception>
    public static DumpVersion Parse(string text)
    {
        if (!TryParse(text, out DumpVersion version))
        {
            throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                "'{0}' is not a version of the form major.minor, such as 0.1.",
                text));
        }

        return version;
    }

    /// <summary>Tries to parse a version of the form <c>major.minor</c>.</summary>
    /// <param name="text">Two decimal numbers without leading zeros, separated by a dot, such as <c>0.1</c>.</param>
    /// <param name="version">The version, or the default value if <paramref name="text"/> is not a version.</param>
    /// <returns>Whether <paramref name="text"/> is a version.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out DumpVersion version)
    {
        version = default;
        if (text is null)
        {
            return false;
        }

        int dot = text.IndexOf('.');
        if (dot < 0
            || !TryParseNumber(text, 0, dot, out int major)
            || !TryParseNumber(text, dot + 1, text.Length, out int minor))
        {
            return false;
        }

        version = new DumpVersion(major, minor);
        return true;
    }

    /// <summary>Returns the version as <c>major.minor</c>.</summary>
    public override string ToString() =>
        Major.ToString(CultureInfo.InvariantCulture) + "." + Minor.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public bool Equals(DumpVersion other) => Major == other.Major && Minor == other.Minor;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DumpVersion other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => unchecked((Major * 397) ^ Minor);

    /// <summary>Orders versions by major, then minor version.</summary>
    /// <param name="other">The version to compare with.</param>
    public int CompareTo(DumpVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <summary>Whether two versions are equal.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator ==(DumpVersion left, DumpVersion right) => left.Equals(right);

    /// <summary>Whether two versions differ.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator !=(DumpVersion left, DumpVersion right) => !left.Equals(right);

    /// <summary>Whether <paramref name="left"/> is older than <paramref name="right"/>.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator <(DumpVersion left, DumpVersion right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is newer than <paramref name="right"/>.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator >(DumpVersion left, DumpVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is older than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator <=(DumpVersion left, DumpVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is newer than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static bool operator >=(DumpVersion left, DumpVersion right) => left.CompareTo(right) >= 0;

    private static bool TryParseNumber(string text, int start, int end, out int value)
    {
        value = 0;
        int length = end - start;
        if (length == 0 || length > MaxDigits || (text[start] == '0' && length > 1))
        {
            return false;
        }

        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (c < '0' || c > '9')
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        return true;
    }
}
