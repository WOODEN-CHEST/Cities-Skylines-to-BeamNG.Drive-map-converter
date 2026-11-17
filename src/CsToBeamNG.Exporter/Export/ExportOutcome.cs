namespace CsToBeamNG.Exporter.Export;

/// <summary>How an export ended, for the message to the user.</summary>
internal sealed class ExportOutcome
{
    /// <summary>Creates an outcome.</summary>
    /// <param name="succeeded">Whether the dump is complete.</param>
    /// <param name="message">The message for the user.</param>
    /// <param name="dumpFolder">The dump's folder, if one was created.</param>
    public ExportOutcome(bool succeeded, string message, string? dumpFolder)
    {
        Succeeded = succeeded;
        Message = message;
        DumpFolder = dumpFolder;
    }

    /// <summary>Whether the dump is complete.</summary>
    public bool Succeeded { get; }

    /// <summary>The message for the user.</summary>
    public string Message { get; }

    /// <summary>The dump's folder, if one was created; an incomplete one when the export failed.</summary>
    public string? DumpFolder { get; }
}
