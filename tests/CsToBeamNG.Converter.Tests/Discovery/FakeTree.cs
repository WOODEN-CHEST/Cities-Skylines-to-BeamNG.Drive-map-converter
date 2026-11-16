using System;
using System.IO;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Converter.Tests.Discovery;

/// <summary>A temporary folder tree for discovery tests. It is deleted on dispose.</summary>
internal sealed class FakeTree : IDisposable
{
    public FakeTree()
    {
        // Resolved, so that expected paths match the locator's link-free paths even if the temp
        // folder sits behind a symbolic link.
        string created = Directory.CreateTempSubdirectory("cstobeamng-tests-").FullName;
        Root = PathTools.ResolveLinks(Path.TrimEndingDirectorySeparator(created));
    }

    public string Root { get; }

    /// <summary>Returns a path below the root without creating anything.</summary>
    public string PathOf(params string[] parts) => Path.Join([Root, .. parts]);

    /// <summary>Creates a folder below the root and returns its path.</summary>
    public string Dir(params string[] parts)
    {
        string path = PathOf(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Leftovers in the temp folder are harmless; a failed clean-up must not fail a test.
        }
    }

    /// <summary>Writes a file, creating its folder, and returns its path.</summary>
    public static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
