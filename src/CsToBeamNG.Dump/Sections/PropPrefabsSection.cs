namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>prop-prefabs</c> section: the prop prefabs that the city's props use (docs/dump-format.md,
/// <c>prop-prefabs</c>; docs/research/cs1-assets.md, Prefabs). <see cref="PropPrefabsSectionWriter"/> writes it;
/// <c>PropPrefabsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class PropPrefabsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "prop-prefabs";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The JSON file with the prefabs, ordered by index.</summary>
    public const string PrefabsFileName = "prefabs.json";

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
