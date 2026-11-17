namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>net-prefabs</c> section: the network prefabs that the city's nodes and segments use (docs/dump-format.md,
/// <c>net-prefabs</c>; docs/research/cs1-networks.md, Prefabs). <see cref="NetPrefabsSectionWriter"/> writes it;
/// <c>NetPrefabsSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class NetPrefabsSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "net-prefabs";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The JSON file with the prefabs, ordered by index.</summary>
    public const string PrefabsFileName = "prefabs.json";

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
