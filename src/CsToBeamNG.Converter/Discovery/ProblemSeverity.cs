namespace CsToBeamNG.Converter.Discovery;

/// <summary>How serious a discovery problem is.</summary>
public enum ProblemSeverity
{
    /// <summary>Worth knowing, nothing to fix: for example, an unused old user folder.</summary>
    Info,

    /// <summary>Something is odd or missing, but conversion can still work.</summary>
    Warning,

    /// <summary>Something essential is missing or wrong; conversion can't work until it is fixed.</summary>
    Error,
}
