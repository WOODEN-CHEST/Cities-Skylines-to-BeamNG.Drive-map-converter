namespace CsToBeamNG.Converter.Loading;

/// <summary>How serious a <see cref="LoadMessage"/> is.</summary>
public enum LoadMessageSeverity
{
    /// <summary>For information, such as a section this version doesn't know.</summary>
    Info,

    /// <summary>The result may lack data, such as a section of an incompatible version or a skipped object.</summary>
    Warning,

    /// <summary>Data that should be there couldn't be read, such as a damaged section.</summary>
    Error,
}
