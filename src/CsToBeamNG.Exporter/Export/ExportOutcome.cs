namespace CsToBeamNG.Exporter.Export;

/// <summary>How an export ended, for the message to the user.</summary>
internal sealed class ExportOutcome
{
    /// <summary>Creates an outcome.</summary>
    /// <param name="succeeded">Whether the dump is complete.</param>
    /// <param name="message">The message for the user.</param>
    public ExportOutcome(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>Whether the dump is complete.</summary>
    public bool Succeeded { get; }

    /// <summary>The message for the user.</summary>
    public string Message { get; }
}
