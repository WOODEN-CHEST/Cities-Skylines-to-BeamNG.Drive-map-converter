using System;

namespace CsToBeamNG.Exporter.Core;

/// <summary>A save the game knows, as <see cref="SaveMatcher"/> sees it.</summary>
internal sealed class SaveCandidate
{
    /// <summary>Creates a candidate.</summary>
    /// <param name="name">The name the Load panel lists (the save asset's name).</param>
    /// <param name="fileName">The package file's name without its extension, if known.</param>
    /// <param name="savedTime">When it was saved: the save's own time stamp, comparable with the other saves'.</param>
    public SaveCandidate(string name, string? fileName, DateTime savedTime)
    {
        Name = name;
        FileName = fileName;
        SavedTime = savedTime;
    }

    /// <summary>The name the Load panel lists.</summary>
    public string Name { get; }

    /// <summary>The package file's name without its extension, if known.</summary>
    public string? FileName { get; }

    /// <summary>When it was saved: the save's own time stamp, comparable with the other saves'.</summary>
    public DateTime SavedTime { get; }

    /// <summary>
    /// The file name without extension from a save package's path, which for Steam Cloud saves is not a
    /// file-system path but <c>cloud:&lt;file&gt;</c> (docs/research/cs1-save-and-world.md, Loading a save
    /// from a mod). Returns <see langword="null"/> for an empty path.
    /// </summary>
    /// <param name="packagePath">The package's path.</param>
    public static string? FileNameFromPackagePath(string? packagePath)
    {
        if (string.IsNullOrEmpty(packagePath))
        {
            return null;
        }

        // Path.GetFileName doesn't treat ':' as a separator on Linux, so split by hand.
        int start = packagePath!.LastIndexOfAny(['/', '\\', ':']) + 1;
        string fileName = packagePath.Substring(start);
        int extension = fileName.LastIndexOf('.');
        string name = extension > 0 ? fileName.Substring(0, extension) : fileName;
        return name.Length == 0 ? null : name;
    }
}
