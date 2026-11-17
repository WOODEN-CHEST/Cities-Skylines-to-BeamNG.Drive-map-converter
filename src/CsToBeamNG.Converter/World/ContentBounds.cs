using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// The rule by which objects bound the area of interest: the bounds of their positions inside the unlocked tiles, where
/// the city can build, or of all of them if the tiles are unknown (docs/architecture.md, D32 and D35).
/// </summary>
internal static class ContentBounds
{
    /// <summary>Returns the bounds of the positions inside the unlocked tiles and how many there are.</summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="items">The objects.</param>
    /// <param name="position">An object's position.</param>
    /// <param name="unlockedTiles">The city's unlocked tiles; empty if unknown, in which case every object counts.</param>
    /// <returns>The bounds, or <see langword="null"/> if no object counts, and the number of objects that count.</returns>
    public static (MapRect? Bounds, int Count) WithinTiles<T>(IReadOnlyList<T> items, Func<T, DumpVector3> position, IReadOnlyList<TileCoordinate> unlockedTiles)
    {
        var tiles = new List<MapRect>(unlockedTiles.Count);
        foreach (TileCoordinate tile in unlockedTiles)
        {
            tiles.Add(MapGeometry.TileBounds(tile));
        }

        float minX = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;
        int count = 0;
        foreach (T item in items)
        {
            DumpVector3 point = position(item);
            float x = point.X;
            float z = point.Z;
            if (tiles.Count > 0 && !tiles.Exists(tile => x >= tile.MinX && x <= tile.MaxX && z >= tile.MinZ && z <= tile.MaxZ))
            {
                continue;
            }

            minX = Math.Min(minX, x);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxZ = Math.Max(maxZ, z);
            count++;
        }

        return count == 0 ? (null, 0) : (new MapRect(minX, minZ, maxX, maxZ), count);
    }
}
