using System;
using System.IO;

namespace CsToBeamNG.Cli.Tests;

/// <summary>A temporary folder for one test. It is deleted on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder() => Root = Directory.CreateTempSubdirectory("cstobeamng-tests-").FullName;

    public string Root { get; }

    /// <summary>Returns a path below the root without creating anything.</summary>
    public string PathOf(params string[] parts) => Path.Join([Root, .. parts]);

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
}
