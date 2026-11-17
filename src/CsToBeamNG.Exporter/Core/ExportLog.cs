using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// Writes <c>export.log</c> in the dump folder (docs/dump-format.md, Diagnostics), and passes every line
/// except details on, normally to the game log.
/// </summary>
/// <remarks>
/// Thread-safe, because an export runs partly on the game's main thread and partly on its simulation
/// thread. Lines written after <see cref="Dispose"/> are still passed on, but no longer go to the file.
/// Each line is flushed at once, so the log survives a crash of the game.
/// </remarks>
internal sealed class ExportLog : IDisposable
{
    /// <summary>The log's file name in the dump folder.</summary>
    public const string FileName = "export.log";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly object _lock = new();
    private readonly Action<ExportLogLevel, string>? _forward;
    private TextWriter? _file;

    private ExportLog(TextWriter file, Action<ExportLogLevel, string>? forward)
    {
        _file = file;
        _forward = forward;
    }

    /// <summary>Creates <c>export.log</c> in a folder, replacing an existing one.</summary>
    /// <param name="folder">The dump folder.</param>
    /// <param name="forward">Receives each line except details, without the timestamp; or <see langword="null"/>.</param>
    public static ExportLog Create(string folder, Action<ExportLogLevel, string>? forward)
    {
        var stream = new FileStream(Path.Combine(folder, FileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        var file = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };
        return new ExportLog(file, forward);
    }

    /// <summary>Writes a detail line, which isn't passed on.</summary>
    /// <param name="message">The message.</param>
    public void Detail(string message) => Write(ExportLogLevel.Detail, message, null);

    /// <summary>Writes a line about progress or results.</summary>
    /// <param name="message">The message.</param>
    public void Info(string message) => Write(ExportLogLevel.Info, message, null);

    /// <summary>Writes a line about a problem that the export works around.</summary>
    /// <param name="message">The message.</param>
    public void Warning(string message) => Write(ExportLogLevel.Warning, message, null);

    /// <summary>
    /// Writes a line about a problem that stops the export. The file also gets the exception's full text;
    /// the line passed on gets its type and message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception, or <see langword="null"/>.</param>
    public void Error(string message, Exception? exception) => Write(ExportLogLevel.Error, message, exception);

    /// <summary>Closes the file.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _file?.Dispose();
            _file = null;
        }
    }

    /// <summary>Formats a line of the file: the UTC time to the millisecond, the level and the message.</summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="level">The level.</param>
    /// <param name="message">The message.</param>
    internal static string FormatLine(DateTime utc, ExportLogLevel level, string message) =>
        utc.ToString(TimestampFormat, CultureInfo.InvariantCulture) + " " + LevelName(level) + " " + message;

    private static string LevelName(ExportLogLevel level) => level switch
    {
        ExportLogLevel.Detail => "detail ",
        ExportLogLevel.Info => "info   ",
        ExportLogLevel.Warning => "warning",
        _ => "error  ",
    };

    private void Write(ExportLogLevel level, string message, Exception? exception)
    {
        string line = FormatLine(DateTime.UtcNow, level, message);
        lock (_lock)
        {
            if (_file is not null)
            {
                try
                {
                    _file.WriteLine(line);
                    if (exception is not null)
                    {
                        _file.WriteLine(exception.ToString());
                    }
                }
                catch (IOException)
                {
                    // A full disk mustn't turn logging into a second failure; the line is still passed on.
                }
            }
        }

        if (level != ExportLogLevel.Detail && _forward is not null)
        {
            _forward(level, exception is null ? message : message + " " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}
