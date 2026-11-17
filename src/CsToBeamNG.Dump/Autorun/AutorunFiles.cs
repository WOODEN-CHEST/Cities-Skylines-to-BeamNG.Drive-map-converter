using System;
using System.IO;
using System.Text;
using System.Threading;

namespace CsToBeamNG.Dump.Autorun;

/// <summary>
/// Where the autorun files are, <c>&lt;CS1 user data&gt;/CsToBeamNG/autorun/</c>, and how they are read and
/// written. The game and the tool run as separate processes, so writes replace whole files and both sides
/// tolerate the other one having a file open (docs/architecture.md, Unattended export).
/// </summary>
public static class AutorunFiles
{
    /// <summary>The folder in <see cref="DumpLocation.ProductFolderName"/> that holds the autorun files.</summary>
    public const string FolderName = "autorun";

    /// <summary>The request (<see cref="AutorunRequest"/>), written by the tool.</summary>
    public const string RequestFileName = "request.txt";

    /// <summary>What the exporter renames the request to when it takes it, so that it acts on it only once.</summary>
    public const string ConsumedRequestFileName = "request.consumed.txt";

    /// <summary>The status (<see cref="AutorunStatus"/>), written by the exporter.</summary>
    public const string StatusFileName = "status.txt";

    private const int Attempts = 5;
    private const int RetryDelayMilliseconds = 100;

    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Returns the autorun folder for a Cities: Skylines user-data folder.</summary>
    /// <param name="userDataFolder">The game's user-data folder.</param>
    public static string GetFolder(string userDataFolder)
    {
        Guard.NotNull(userDataFolder, nameof(userDataFolder));
        return Path.Combine(Path.Combine(userDataFolder, DumpLocation.ProductFolderName), FolderName);
    }

    /// <summary>
    /// Reads a file as UTF-8, allowing the other process to replace it meanwhile. Returns
    /// <see langword="null"/> if it doesn't exist. Other I/O errors are retried briefly, then thrown.
    /// </summary>
    /// <param name="path">The file.</param>
    public static string? ReadText(string path)
    {
        Guard.NotNull(path, nameof(path));
        return Retry(() =>
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                // FileShare.Delete lets the writer replace the file while it is open here (Windows).
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, _utf8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Writes a file as UTF-8 without a byte order mark: first to a temporary file next to it, which then
    /// replaces it, so a reader sees the old text, the new text, or briefly no file, but never a partial
    /// one. The folder must exist. I/O errors are retried briefly, then thrown.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    public static void WriteText(string path, string text)
    {
        Guard.NotNull(path, nameof(path));
        Guard.NotNull(text, nameof(text));
        string temporary = path + ".tmp";
        Retry(() =>
        {
            File.WriteAllText(temporary, text, _utf8);
            Replace(temporary, path);
            return true;
        });
    }

    /// <summary>
    /// Renames a file, replacing the destination if it exists. I/O errors are retried briefly, then thrown.
    /// </summary>
    /// <param name="source">The file to rename.</param>
    /// <param name="destination">Its new path.</param>
    public static void Move(string source, string destination)
    {
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(destination, nameof(destination));
        Retry(() =>
        {
            Replace(source, destination);
            return true;
        });
    }

    /// <summary>Deletes a file if it exists. I/O errors are retried briefly, then thrown.</summary>
    /// <param name="path">The file.</param>
    public static void Delete(string path)
    {
        Guard.NotNull(path, nameof(path));
        Retry(() =>
        {
            File.Delete(path);
            return true;
        });
    }

    private static void Replace(string source, string destination)
    {
#if NET
        File.Move(source, destination, overwrite: true);
#else
        // .NET 3.5 can't move onto an existing file, so there is a moment without the file.
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(source, destination);
#endif
    }

    // The other process may have the file open for a moment, which fails on Windows. No exception
    // filters: this also runs in the game, whose old Mono runtime isn't known to support them.
    private static T Retry<T>(Func<T> action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException)
            {
                if (attempt >= Attempts)
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt >= Attempts)
                {
                    throw;
                }
            }

            Thread.Sleep(RetryDelayMilliseconds);
        }
    }
}
