using System;
using System.IO;
using CsToBeamNG.Dump.Autorun;
using CsToBeamNG.Exporter.Game;

namespace CsToBeamNG.Exporter.Autorun;

/// <summary>Writes the autorun status file for the tool that made the request. Main thread only.</summary>
internal sealed class StatusReporter
{
    private readonly string _path;

    /// <summary>Creates a reporter.</summary>
    /// <param name="path">The status file.</param>
    public StatusReporter(string path) => _path = path;

    /// <summary>Replaces the status file. Failures are logged, never thrown: the export goes on without it.</summary>
    /// <param name="status">The status.</param>
    public void Report(AutorunStatus status)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            AutorunFiles.WriteText(_path, status.ToText());
        }
        catch (Exception e)
        {
            GameLog.Warning("Autorun: the status (" + AutorunStatus.GetStateName(status.State) + ") couldn't be written to " + _path + ": " + e.Message);
        }
    }
}
