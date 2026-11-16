using System;
using System.Collections.Generic;
using System.IO;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Path handling for the locator. Comparisons follow the simulated OS (case-insensitive on
/// Windows); file-system access always uses the real one.
/// </summary>
internal sealed class PathTools
{
    // Guards against symbolic-link loops; real chains are a few links long.
    private const int MaxLinkHops = 40;

    private static readonly char[] _separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly DiscoveryContext _context;

    public PathTools(DiscoveryContext context) => _context = context;

    /// <summary>
    /// <c>$XDG_DATA_HOME</c>, or <c>~/.local/share</c> if it is unset or relative (relative values
    /// are invalid under the XDG Base Directory specification).
    /// </summary>
    public string DataHome => XdgOrDefault(_context.XdgDataHome, ".local", "share");

    /// <summary><c>$XDG_CONFIG_HOME</c>, or <c>~/.config</c> if it is unset or relative.</summary>
    public string ConfigHome => XdgOrDefault(_context.XdgConfigHome, ".config");

    /// <summary>
    /// The data folders that games see when Steam runs as a Flatpak (inferred, not verified; see
    /// docs/research/game-locations.md).
    /// </summary>
    public IEnumerable<string> FlatpakSteamDataHomes
    {
        get
        {
            string appDir = Path.Join(_context.HomeDir, ".var", "app", "com.valvesoftware.Steam");
            yield return Path.Join(appDir, "data");
            yield return Path.Join(appDir, ".local", "share");
        }
    }

    public bool AreSame(string first, string second) =>
        string.Equals(first, second, _context.Os == OsKind.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Makes a path absolute: expands a leading <c>~</c>, resolves it against the working folder and
    /// removes trailing separators. Symbolic links are kept.
    /// </summary>
    public string Normalize(string path)
    {
        string expanded = ExpandHome(path.Trim());
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded, _context.WorkingDir));
    }

    /// <summary>
    /// Normalizes the path and resolves symbolic links in all its components, so that two spellings
    /// of one folder compare equal. On Linux, <c>~/.steam/steam</c> is usually a link to the real
    /// Steam folder.
    /// </summary>
    public string Canonicalize(string path) => ResolveLinks(Normalize(path));

    /// <summary>Turns a user-given location into a located path. A missing path is an error.</summary>
    public FoundPath FromOverride(PathOverride value, string what, List<DiscoveryProblem> problems)
    {
        string path = Normalize(value.Path);
        bool exists = Directory.Exists(path);
        if (!exists)
        {
            problems.AddError($"{what}: {value.Origin} is set to '{value.Path}', which does not exist.");
        }

        return new FoundPath(path, LocationSource.Override, exists, value.Origin);
    }

    internal static string ResolveLinks(string fullPath)
    {
        string current = fullPath;
        for (int hop = 0; hop < MaxLinkHops; hop++)
        {
            string? next = ResolveFirstLink(current);
            if (next is null)
            {
                return current;
            }

            current = next;
        }

        // A link loop: keep the last path instead of failing discovery.
        return current;
    }

    private static string? ResolveFirstLink(string fullPath)
    {
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string[] parts = fullPath[root.Length..].Split(_separators, StringSplitOptions.RemoveEmptyEntries);
        string prefix = root;
        for (int i = 0; i < parts.Length; i++)
        {
            prefix = Path.Join(prefix, parts[i]);
            string? target = GetLinkTarget(prefix);
            if (target is null)
            {
                continue;
            }

            // A relative link target is relative to the folder that contains the link.
            string linkDir = Path.GetDirectoryName(prefix) ?? root;
            string resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target, linkDir));
            string rest = string.Join(Path.DirectorySeparatorChar, parts, i + 1, parts.Length - i - 1);
            return rest.Length == 0 ? resolved : Path.Join(resolved, rest);
        }

        return null;
    }

    private static string? GetLinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string XdgOrDefault(string? value, params string[] defaultBelowHome) =>
        !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value)
            ? Path.TrimEndingDirectorySeparator(value)
            : Path.Join([_context.HomeDir, .. defaultBelowHome]);

    private string ExpandHome(string path)
    {
        if (path == "~")
        {
            return _context.HomeDir;
        }

        bool startsWithHome = path.Length > 1 && path[0] == '~' && Array.IndexOf(_separators, path[1]) >= 0;
        return startsWithHome ? Path.Join(_context.HomeDir, path[2..]) : path;
    }
}
