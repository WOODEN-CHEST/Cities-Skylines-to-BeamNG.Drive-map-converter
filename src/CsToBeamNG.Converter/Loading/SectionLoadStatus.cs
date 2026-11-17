namespace CsToBeamNG.Converter.Loading;

/// <summary>What happened to a dump section during loading.</summary>
public enum SectionLoadStatus
{
    /// <summary>Its loader read it into the world model.</summary>
    Loaded,

    /// <summary>No loader knows it; it was skipped with an information message.</summary>
    Unknown,

    /// <summary>Its major version differs from the loader's; it was skipped with a warning.</summary>
    Incompatible,

    /// <summary>Its loader found it damaged; it was skipped with an error.</summary>
    Failed,
}
