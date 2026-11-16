using System;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Reads the simple <c>key = value</c> files of BeamNG.drive (<c>BeamNG.drive.ini</c>,
/// <c>startup.ini</c>). See docs/research/game-locations.md.
/// </summary>
internal static class IniFile
{
    /// <summary>
    /// Returns the value of the first <c>key = value</c> line with the key, compared
    /// case-insensitively, or <see langword="null"/>. Lines starting with <c>;</c> or <c>#</c> are
    /// comments, <c>[section]</c> lines are ignored, and the value may be empty.
    /// </summary>
    public static string? ReadValue(string text, string key)
    {
        foreach (string rawLine in text.Split('\n'))
        {
            // Trim also removes the '\r' of Windows line endings; the files start with a byte order mark.
            string line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] is ';' or '#' or '[')
            {
                continue;
            }

            int equals = line.IndexOf('=');
            if (equals > 0 && string.Equals(line[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(equals + 1)..].Trim();
            }
        }

        return null;
    }
}
