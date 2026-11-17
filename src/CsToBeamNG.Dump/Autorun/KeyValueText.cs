using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsToBeamNG.Dump.Autorun;

/// <summary>
/// The line format of the autorun files: one <c>key=value</c> per line, <c>\n</c> line endings, blank lines
/// and lines starting with <c>#</c> ignored. Keys are lowercase ASCII letters, digits and hyphens, starting
/// with a letter. Values run to the end of the line; a backslash, a line feed and a carriage return in a
/// value are written as <c>\\</c>, <c>\n</c> and <c>\r</c>. A plain format rather than JSON because the
/// exporter, which reads requests, runs on .NET 3.5, which has no JSON reader (docs/architecture.md,
/// Unattended export).
/// </summary>
internal static class KeyValueText
{
    /// <summary>Writes the pairs in the given order.</summary>
    public static string Format(IList<KeyValuePair<string, string>> pairs)
    {
        var text = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            if (!IsValidKey(pair.Key))
            {
                throw new ArgumentException("'" + pair.Key + "' is not a valid key.", nameof(pairs));
            }

            text.Append(pair.Key).Append('=').Append(Escape(pair.Value)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Reads the pairs. A line without <c>=</c>, an invalid key, a key used twice or an unknown escape
    /// throws a <see cref="FormatException"/>. A carriage return before a line feed is ignored.
    /// </summary>
    public static Dictionary<string, string> Parse(string text)
    {
        Guard.NotNull(text, nameof(text));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length > 0 && line[line.Length - 1] == '\r')
            {
                line = line.Substring(0, line.Length - 1);
            }

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string lineNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                throw new FormatException("Line " + lineNumber + " has no '='.");
            }

            string key = line.Substring(0, separator);
            if (!IsValidKey(key))
            {
                throw new FormatException("Line " + lineNumber + ": '" + key + "' is not a valid key.");
            }

            if (values.ContainsKey(key))
            {
                throw new FormatException("Line " + lineNumber + ": the key '" + key + "' is used twice.");
            }

            values.Add(key, Unescape(line.Substring(separator + 1), lineNumber));
        }

        return values;
    }

    /// <summary>Whether a key follows the rules: lowercase ASCII letters, digits and hyphens, starting with a letter.</summary>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (char c in key)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Escape(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                default:
                    escaped.Append(c);
                    break;
            }
        }

        return escaped.ToString();
    }

    private static string Unescape(string value, string lineNumber)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var unescaped = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c != '\\')
            {
                unescaped.Append(c);
                continue;
            }

            char next = i + 1 < value.Length ? value[i + 1] : '\0';
            unescaped.Append(next switch
            {
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                _ => throw new FormatException("Line " + lineNumber + " has an invalid escape; use \\\\, \\n or \\r."),
            });
            i++;
        }

        return unescaped.ToString();
    }
}
