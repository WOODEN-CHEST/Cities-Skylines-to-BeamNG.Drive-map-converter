using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Install;

/// <summary>
/// Installs generated files as an unpacked mod: <c>&lt;BeamNG user folder&gt;/mods/unpacked/&lt;mod&gt;/</c>, which the
/// game mounts at the root of its virtual file system.
/// </summary>
/// <remarks>
/// <para>
/// The installer only touches folders named <c>cstobeamng_*</c> (<see cref="BeamNGNames.IsOwnModFolderName"/>), and
/// in them only the files it wrote itself. It lists those in <see cref="FileListName"/>. Other files, added by the
/// user or the game, stay. A folder that exists without the list, and isn't empty, is refused: it wasn't made by
/// this tool.
/// </para>
/// <para>
/// The game watches mod folders and reloads changed files while it runs (docs/research/beamng-level-format.md,
/// Where levels live). So each file is written under a temporary name and then renamed over the old one, and only
/// then are files of the previous install that are no longer produced deleted. While this happens, the list names
/// the old and the new files, so an interruption never leaves an untracked file behind.
/// </para>
/// </remarks>
public static class ModInstaller
{
    /// <summary>The file in the mod folder that lists the files this tool wrote.</summary>
    public const string FileListName = "cstobeamng-files.json";

    private const string GeneratorName = "cstobeamng";
    private const string TemporarySuffix = ".cstobeamng-tmp";

    /// <summary>Installs the files, replacing the previous install of the same mod.</summary>
    /// <param name="unpackedModsDir">The <c>mods/unpacked</c> folder; it is created if missing.</param>
    /// <param name="modFolderName">The mod folder's name, which starts with <see cref="BeamNGNames.ModFolderPrefix"/>.</param>
    /// <param name="files">The files, at distinct paths.</param>
    /// <returns>What was written and removed.</returns>
    /// <exception cref="ArgumentException">The name isn't one of ours, or the files clash.</exception>
    /// <exception cref="InstallException">The mod folder wasn't made by this tool, or its file list is invalid.</exception>
    /// <exception cref="IOException">Writing or deleting failed, for example because the game holds a file open.</exception>
    public static InstallResult Install(string unpackedModsDir, string modFolderName, IReadOnlyList<OutputFile> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(unpackedModsDir);
        ArgumentNullException.ThrowIfNull(files);
        if (!BeamNGNames.IsOwnModFolderName(modFolderName))
        {
            throw new ArgumentException(
                $"'{modFolderName}' isn't a mod folder name of this tool: it must start with '{BeamNGNames.ModFolderPrefix}'.",
                nameof(modFolderName));
        }

        List<string> relativePaths = CheckFiles(files);
        string modDir = Path.Join(Path.GetFullPath(unpackedModsDir), modFolderName);
        IReadOnlyList<string> previous = ReadPreviousFileList(modDir);

        Directory.CreateDirectory(modDir);
        var tracked = new SortedSet<string>(previous, StringComparer.Ordinal);
        tracked.UnionWith(relativePaths);
        WriteFileList(modDir, tracked);

        long bytes = 0;
        int replaced = 0;
        foreach (OutputFile file in files)
        {
            string path = file.Path.ToFileSystemPath(modDir);
            if (File.Exists(path))
            {
                replaced++;
            }

            bytes += WriteFile(file, path);
        }

        // Compared without regard to case: on Windows a differently cased old path is the same file as the new one.
        var current = new HashSet<string>(relativePaths, StringComparer.OrdinalIgnoreCase);
        var stale = new List<string>();
        foreach (string path in previous)
        {
            if (!current.Contains(path))
            {
                stale.Add(path);
            }
        }

        int removed = RemoveFiles(modDir, stale);
        WriteFileList(modDir, relativePaths);
        return new InstallResult(modDir, files.Count, bytes, replaced, removed);
    }

    /// <summary>Reads the file list of an installed mod.</summary>
    /// <param name="modDir">The mod folder.</param>
    /// <returns>The listed paths, relative to the mod folder with <c>/</c> separators; empty if there is no mod folder.</returns>
    /// <exception cref="InstallException">The folder isn't empty but has no valid list.</exception>
    public static IReadOnlyList<string> ReadPreviousFileList(string modDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(modDir);
        string listPath = Path.Join(modDir, FileListName);
        if (!File.Exists(listPath))
        {
            if (Directory.Exists(modDir) && !IsEmptyDirectory(modDir))
            {
                throw new InstallException(
                    $"'{modDir}' exists but wasn't created by this tool (it has no {FileListName}). Move or delete it yourself.");
            }

            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(listPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("generator", out JsonElement generator)
                || generator.ValueKind != JsonValueKind.String
                || generator.GetString() != GeneratorName
                || !root.TryGetProperty("files", out JsonElement filesElement)
                || filesElement.ValueKind != JsonValueKind.Array)
            {
                throw new InstallException($"'{listPath}' isn't a file list of this tool.");
            }

            var paths = new List<string>();
            foreach (JsonElement entry in filesElement.EnumerateArray())
            {
                string? path = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
                if (path is null || !IsSafeRelativePath(path))
                {
                    throw new InstallException($"'{listPath}' lists an invalid path: {entry.GetRawText()}.");
                }

                paths.Add(path);
            }

            return paths;
        }
        catch (JsonException e)
        {
            throw new InstallException($"'{listPath}' isn't valid JSON: {e.Message}", e);
        }
    }

    private static List<string> CheckFiles(IReadOnlyList<OutputFile> files)
    {
        // Distinct even without regard to case, so the install works the same on Windows and macOS file systems.
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relativePaths = new List<string>();
        foreach (OutputFile file in files)
        {
            string relative = file.Path.RelativeTo(VirtualPath.Root);
            if (string.Equals(relative, FileListName, StringComparison.OrdinalIgnoreCase) || !paths.Add(relative))
            {
                throw new ArgumentException($"The file '{file.Path}' is listed twice or clashes with {FileListName}.", nameof(files));
            }

            folders.UnionWith(FoldersOf(file.Path));
            relativePaths.Add(relative);
        }

        foreach (string relative in relativePaths)
        {
            if (folders.Contains(relative))
            {
                throw new ArgumentException($"'{relative}' is both a file and a folder.", nameof(files));
            }
        }

        relativePaths.Sort(StringComparer.Ordinal);
        return relativePaths;
    }

    private static int RemoveFiles(string modDir, IReadOnlyList<string> relativePaths)
    {
        int removed = 0;
        var folders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string relative in relativePaths)
        {
            VirtualPath path = VirtualPath.Root.Combine(relative);
            if (PassesThroughLink(modDir, path))
            {
                // Following a link could delete files outside the mod folder.
                continue;
            }

            string fullPath = path.ToFileSystemPath(modDir);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                removed++;
            }

            folders.UnionWith(FoldersOf(path));
        }

        // Deepest folders first: a child's path extends its parent's, so it sorts after it. A folder that still holds
        // anything, such as the game's caches, stays.
        foreach (string folder in folders.Reverse())
        {
            string fullPath = VirtualPath.Root.Combine(folder).ToFileSystemPath(modDir);
            if (Directory.Exists(fullPath) && IsEmptyDirectory(fullPath))
            {
                Directory.Delete(fullPath);
            }
        }

        return removed;
    }

    // The folders that contain a path, as relative paths, such as "levels" and "levels/x" for "/levels/x/info.json".
    private static IEnumerable<string> FoldersOf(VirtualPath path)
    {
        for (VirtualPath? folder = path.Parent; folder is not null && folder.Segments.Count > 0; folder = folder.Parent)
        {
            yield return folder.RelativeTo(VirtualPath.Root);
        }
    }

    private static bool IsEmptyDirectory(string path)
    {
        using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        return !entries.MoveNext();
    }

    private static bool PassesThroughLink(string modDir, VirtualPath path)
    {
        string current = modDir;
        for (int i = 0; i < path.Segments.Count - 1; i++)
        {
            current = Path.Join(current, path.Segments[i]);
            if (new DirectoryInfo(current).LinkTarget is not null)
            {
                return true;
            }
        }

        return false;
    }

    // Writes under a temporary name, then renames over the target, so readers see the old or the new file, never
    // a part of one. Returns the file's length.
    private static long WriteFile(OutputFile file, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + TemporarySuffix;
        try
        {
            long length;
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.WriteTo(stream);
                length = stream.Length;
            }

            File.Move(temporaryPath, path, overwrite: true);
            return length;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void WriteFileList(string modDir, IEnumerable<string> relativePaths)
    {
        var list = new JsonArray();
        foreach (string path in relativePaths)
        {
            list.Add(path);
        }

        var document = new JsonObject
        {
            ["generator"] = GeneratorName,
            ["files"] = list,
        };
        var listFile = new OutputFile(VirtualPath.Root.Combine(FileListName), stream => BeamNGJson.WriteDocument(stream, document));
        WriteFile(listFile, Path.Join(modDir, FileListName));
    }

    private static bool IsSafeRelativePath(string path)
    {
        try
        {
            VirtualPath.Root.Combine(path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
