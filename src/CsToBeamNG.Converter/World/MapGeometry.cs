using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// The fixed geometry of a Cities: Skylines map: 9 × 9 tiles of 1920 m, centred on the origin, so the map spans
/// −8640 to +8640 m on X and Z (docs/research/cs1-save-and-world.md, World geometry).
/// </summary>
/// <remarks>
/// Tiles are numbered as in the dump (docs/dump-format.md, <c>city</c>): tile (x, z) covers X from
/// −8640 + 1920 · x to −8640 + 1920 · (x + 1) metres, and Z likewise, so the centre tile is (4, 4).
/// </remarks>
public static class MapGeometry
{
    /// <summary>The edge length of a tile in metres (docs/research/cs1-save-and-world.md, World geometry).</summary>
    public const float TileSizeMeters = 1920;

    /// <summary>The number of tiles along each axis of the map (docs/research/cs1-save-and-world.md, World geometry).</summary>
    public const int TilesPerSide = TileCoordinate.GridSize;

    /// <summary>The map's edge length in metres: 17,280.</summary>
    public const float SizeMeters = TileSizeMeters * TilesPerSide;

    /// <summary>
    /// Half the map's edge length in metres: the map spans −8640 to +8640 on X and Z
    /// (docs/research/cs1-save-and-world.md, World geometry).
    /// </summary>
    public const float HalfSizeMeters = SizeMeters / 2;

    /// <summary>The whole map.</summary>
    public static MapRect Bounds { get; } = new(-HalfSizeMeters, -HalfSizeMeters, HalfSizeMeters, HalfSizeMeters);

    /// <summary>Returns the area a tile covers.</summary>
    /// <param name="tile">The tile.</param>
    public static MapRect TileBounds(TileCoordinate tile)
    {
        float minX = -HalfSizeMeters + (tile.X * TileSizeMeters);
        float minZ = -HalfSizeMeters + (tile.Z * TileSizeMeters);
        return new MapRect(minX, minZ, minX + TileSizeMeters, minZ + TileSizeMeters);
    }

    /// <summary>Returns the smallest rectangle that covers some tiles.</summary>
    /// <param name="tiles">The tiles, in any order; duplicates don't matter.</param>
    /// <returns>The rectangle, or <see langword="null"/> if there are no tiles.</returns>
    public static MapRect? TileBounds(IEnumerable<TileCoordinate> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        MapRect? bounds = null;
        foreach (TileCoordinate tile in tiles)
        {
            MapRect tileBounds = TileBounds(tile);
            bounds = bounds?.Union(tileBounds) ?? tileBounds;
        }

        return bounds;
    }
}
