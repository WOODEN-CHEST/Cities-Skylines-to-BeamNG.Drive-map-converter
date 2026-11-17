namespace CsToBeamNG.Converter.Loading;

/// <summary>Options for <see cref="WorldLoader.Load(string, WorldLoadOptions?)"/>.</summary>
public sealed record WorldLoadOptions
{
    /// <summary>The defaults: incomplete dumps are rejected, and the converter's own loaders are used.</summary>
    public static WorldLoadOptions Default { get; } = new();

    /// <summary>
    /// Whether to load a dump whose export didn't finish, with a warning instead of an error. For diagnostics, such
    /// as <c>cstobeamng inspect</c>; such a dump may lack data.
    /// </summary>
    public bool AllowIncomplete { get; init; }

    /// <summary>The loaders to use, or <see langword="null"/> for <see cref="SectionLoaderRegistry.CreateDefault"/>.</summary>
    public SectionLoaderRegistry? Loaders { get; init; }
}
