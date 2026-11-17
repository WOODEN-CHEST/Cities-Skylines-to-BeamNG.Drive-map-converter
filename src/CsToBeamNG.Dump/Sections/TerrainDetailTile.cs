using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Dump.Sections;

/// <summary>The detail heights of one map tile (docs/dump-format.md, <c>terrain</c>).</summary>
public sealed class TerrainDetailTile
{
    /// <summary>Creates a tile's detail heights.</summary>
    /// <param name="tile">The map tile.</param>
    /// <param name="heights">
    /// The heights in 1/64 m (<see cref="TerrainSection.MetersPerHeightUnit"/>): tile resolution squared points, row
    /// by row, starting at the tile's −X, −Z corner.
    /// </param>
    public TerrainDetailTile(TileCoordinate tile, ushort[] heights)
    {
        Guard.NotNull(heights, nameof(heights));
        Tile = tile;
        Heights = heights;
    }

    /// <summary>The map tile.</summary>
    public TileCoordinate Tile { get; }

    /// <summary>The heights in 1/64 m, row by row from the tile's −X, −Z corner.</summary>
    public ushort[] Heights { get; }
}
