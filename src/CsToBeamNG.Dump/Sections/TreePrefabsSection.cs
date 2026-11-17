namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>tree-prefabs</c> section: the tree prefabs that the city's trees use (docs/dump-format.md,
/// <c>tree-prefabs</c>; docs/research/cs1-assets.md, Prefabs). <see cref="TreePrefabsSectionWriter"/> writes it;
/// <c>TreePrefabsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class TreePrefabsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "tree-prefabs";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The JSON file with the prefabs, ordered by index.</summary>
    public const string PrefabsFileName = "prefabs.json";

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
