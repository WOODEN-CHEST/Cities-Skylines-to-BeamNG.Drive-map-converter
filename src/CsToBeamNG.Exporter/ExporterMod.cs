using ICities;

namespace CsToBeamNG.Exporter;

/// <summary>
/// The mod's entry point. Cities: Skylines lists every <see cref="IUserMod"/> it finds in the mods
/// folder in Content Manager → Mods.
/// </summary>
public sealed class ExporterMod : IUserMod
{
    /// <inheritdoc/>
    public string Name => "CS to BeamNG Exporter";

    /// <inheritdoc/>
    public string Description => "Exports the loaded city for conversion into a BeamNG.drive level.";
}
