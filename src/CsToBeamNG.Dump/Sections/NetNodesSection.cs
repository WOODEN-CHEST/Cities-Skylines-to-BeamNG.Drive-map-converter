namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>net-nodes</c> section: every node of the game's networks (docs/dump-format.md, <c>net-nodes</c>;
/// docs/research/cs1-networks.md, Data model). <see cref="NetNodesSectionWriter"/> writes it; <c>NetNodesSectionReader</c>
/// reads it in the .NET 10 build.
/// </summary>
public static class NetNodesSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "net-nodes";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The nodes, ordered by id.</summary>
    public const string NodesFileName = "nodes.bin";

    /// <summary>The record type of <see cref="NodesFileName"/>.</summary>
    public const string NodeRecordType = "netNode";

    /// <summary>The size of a <see cref="NodeRecordType"/> record in bytes.</summary>
    public const int NodeRecordSize = 68;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
