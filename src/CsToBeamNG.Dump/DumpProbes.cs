namespace CsToBeamNG.Dump;

/// <summary>
/// Developer probes: extra data that the exporter writes on request next to a dump, for research and
/// validation. Probes are not part of the dump format; readers ignore their folder
/// (docs/dump-format.md, Probes).
/// </summary>
public static class DumpProbes
{
    /// <summary>The folder inside a dump folder that holds one folder per probe.</summary>
    public const string FolderName = "probes";

    /// <summary>
    /// The geometry probe: the game's road meshes, the parameters it bends them with, and the result of its
    /// own CPU bending, for a few automatically chosen segments and nodes
    /// (docs/research/cs1-networks.md, Bending network meshes).
    /// </summary>
    public const string Geometry = "geometry";

    /// <summary>The geometry probe's file, in its folder.</summary>
    public const string GeometryFileName = "geometry-probe.json";

    /// <summary>
    /// The heights probe: the game's own terrain height samplers evaluated at road nodes, road segments, buildings and
    /// chosen points, for comparing the dumped terrain and the converter's sampling with the ground the game uses
    /// (docs/research/cs1-save-and-world.md, Terrain).
    /// </summary>
    public const string Heights = "heights";

    /// <summary>The heights probe's file, in its folder.</summary>
    public const string HeightsFileName = "heights-probe.json";

    /// <summary>Whether a probe name is valid: lowercase ASCII letters, digits and hyphens, starting with a letter.</summary>
    /// <param name="name">The name.</param>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name![0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (char c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
