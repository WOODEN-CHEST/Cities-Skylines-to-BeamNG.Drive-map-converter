namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>terrain</c> section: the game's terrain heights on the 16 m grid, raw and final, and the 4 m detail heights
/// of the tiles that have them (docs/dump-format.md, <c>terrain</c>; docs/research/cs1-save-and-world.md, Terrain).
/// <see cref="TerrainSectionWriter"/> writes it; <c>TerrainSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class TerrainSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "terrain";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The JSON file with the grids' geometry and the list of tiles with detail heights.</summary>
    public const string InfoFileName = "terrain.json";

    /// <summary>The raw heights: the terrain as the map and the landscaping tools shaped it.</summary>
    public const string RawHeightsFileName = "raw-heights.bin";

    /// <summary>The final heights: the raw heights after roads and buildings have levelled the ground.</summary>
    public const string FinalHeightsFileName = "final-heights.bin";

    /// <summary>The detail heights of the listed tiles, tile after tile.</summary>
    public const string DetailHeightsFileName = "detail-heights.bin";

    /// <summary>The record type of every height file: one <c>u16</c> height.</summary>
    public const string HeightRecordType = "terrainHeight";

    /// <summary>The size of a <see cref="HeightRecordType"/> record in bytes.</summary>
    public const int HeightRecordSize = 2;

    /// <summary>
    /// The height of one unit of a stored height, in metres: heights are 1/64 m steps from 0 to 1023.98 m
    /// (docs/research/cs1-save-and-world.md, Terrain).
    /// </summary>
    public const float MetersPerHeightUnit = 1f / 64;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
