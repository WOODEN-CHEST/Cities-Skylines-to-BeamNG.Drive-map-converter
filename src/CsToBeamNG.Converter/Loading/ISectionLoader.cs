using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Converter.Loading;

/// <summary>
/// Reads one kind of dump section into the world model. Each section type that the converter understands has one,
/// registered in <see cref="SectionLoaderRegistry.CreateDefault"/>.
/// </summary>
/// <remarks>
/// The section's files and record layouts are defined once in <c>CsToBeamNG.Dump</c> (docs/dump-format.md,
/// Sections); the loader uses those definitions and turns the records into its <see cref="ISectionData"/>.
/// <see cref="WorldLoader"/> checks the version before calling it.
/// </remarks>
public interface ISectionLoader
{
    /// <summary>The name of the section it reads, such as <c>terrain</c>.</summary>
    string SectionName { get; }

    /// <summary>
    /// The section version the loader was written for. It is given every version with the same major version;
    /// for an older minor version it treats the fields added since as absent (docs/dump-format.md, Versioning).
    /// </summary>
    DumpVersion Version { get; }

    /// <summary>Reads the section.</summary>
    /// <param name="section">The section, whose major version matches <see cref="Version"/>.</param>
    /// <param name="context">Where to report problems it works around, such as skipped objects, and the data loaded so far.</param>
    /// <returns>The section's data.</returns>
    /// <exception cref="DumpException">The section is damaged; it is skipped and the error reported.</exception>
    ISectionData Load(DumpSection section, SectionLoadContext context);
}
