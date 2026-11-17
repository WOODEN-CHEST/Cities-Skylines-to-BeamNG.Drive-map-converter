namespace CsToBeamNG.Exporter.Core;

/// <summary>How important an <see cref="ExportLog"/> line is.</summary>
internal enum ExportLogLevel
{
    /// <summary>Details for diagnosis, such as each object that was skipped. Only in <c>export.log</c>.</summary>
    Detail,

    /// <summary>Progress and results. Also passed on to the game log.</summary>
    Info,

    /// <summary>A problem that the export works around. Also passed on to the game log.</summary>
    Warning,

    /// <summary>A problem that stops the export. Also passed on to the game log.</summary>
    Error,
}
