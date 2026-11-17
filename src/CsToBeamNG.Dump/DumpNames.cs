using System.IO;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump;

/// <summary>The naming rules for sections, files, record types and keys (docs/dump-format.md, Names).</summary>
internal static class DumpNames
{
    public const int MaxSectionNameLength = 64;

    /// <summary>
    /// The longest <c>section/path</c> of a file. It keeps full paths within Windows' 260-character
    /// limit, which .NET 3.5 enforces, even below a long user-data folder.
    /// </summary>
    public const int MaxDumpPathLength = 120;

    public const int MaxKeyLength = 64;

    public const string SectionNameRule =
        "use lowercase letters and digits, in words joined by single hyphens, starting with a letter, such as 'net-nodes'";

    public const string FilePathRule =
        "use segments of lowercase letters, digits, '_', '-' and '.', separated by '/', each starting and ending with a letter, digit or '_'";

    public const string RecordTypeRule =
        "use 1 to 40 ASCII letters, digits, '_', '-' and '.', starting with a letter, such as 'netNode'";

    public const string KeyRule = "use a camelCase identifier: ASCII letters and digits, starting with a lowercase letter";

    // Windows treats these names as devices, with or without an extension, so files can't have them.
    private static readonly string[] _reservedNames =
    [
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    public static bool IsValidSectionName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name!.Length > MaxSectionNameLength
            || !IsLowercaseLetter(name[0]) || name[name.Length - 1] == '-')
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '-' ? name[i - 1] == '-' : !IsLowercaseLetter(c) && !IsDigit(c))
            {
                return false;
            }
        }

        // The probes folder lies next to the sections (docs/dump-format.md, Layout).
        return !IsReserved(name, 0, name.Length) && name != DumpProbes.FolderName;
    }

    /// <summary>Checks a file's path within its section folder.</summary>
    public static bool IsValidFilePath(string sectionName, string? path)
    {
        if (string.IsNullOrEmpty(path) || sectionName.Length + 1 + path!.Length > MaxDumpPathLength)
        {
            return false;
        }

        int start = 0;
        while (true)
        {
            int end = path.IndexOf('/', start);
            if (end < 0)
            {
                return IsValidSegment(path, start, path.Length);
            }

            if (!IsValidSegment(path, start, end))
            {
                return false;
            }

            start = end + 1;
        }
    }

    public static bool IsValidRecordType(string? name)
    {
        if (string.IsNullOrEmpty(name) || name!.Length > RecordFileFormat.MaxRecordTypeLength || !IsLetter(name[0]))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!IsLetter(c) && !IsDigit(c) && c != '_' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key!.Length > MaxKeyLength || !IsLowercaseLetter(key[0]))
        {
            return false;
        }

        foreach (char c in key)
        {
            if (!IsLetter(c) && !IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Turns a valid <c>/</c>-separated path into a file-system path below <paramref name="folder"/>.</summary>
    public static string ToFileSystemPath(string folder, string path) =>
        Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));

    private static bool IsValidSegment(string path, int start, int end)
    {
        if (end == start || !IsSegmentEdge(path[start]) || !IsSegmentEdge(path[end - 1]))
        {
            return false;
        }

        // Windows looks at the part before the first dot when it checks for device names.
        int baseNameEnd = end;
        for (int i = start; i < end; i++)
        {
            char c = path[i];
            if (c == '.')
            {
                baseNameEnd = i < baseNameEnd ? i : baseNameEnd;
            }
            else if (c != '-' && !IsSegmentEdge(c))
            {
                return false;
            }
        }

        return !IsReserved(path, start, baseNameEnd);
    }

    private static bool IsReserved(string text, int start, int end)
    {
        int length = end - start;
        foreach (string reserved in _reservedNames)
        {
            if (reserved.Length == length && string.CompareOrdinal(text, start, reserved, 0, length) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSegmentEdge(char c) => IsLowercaseLetter(c) || IsDigit(c) || c == '_';

    private static bool IsLowercaseLetter(char c) => c is >= 'a' and <= 'z';

    private static bool IsLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
