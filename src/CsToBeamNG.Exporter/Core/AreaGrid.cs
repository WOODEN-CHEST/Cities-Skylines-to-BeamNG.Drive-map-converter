using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// Reads the game's grid of unlocked map tiles, <c>GameAreaManager.m_areaGrid</c>
/// (docs/research/cs1-save-and-world.md, Map tiles).
/// </summary>
/// <remarks>
/// The grid is square, stored row by row (index <c>z * size + x</c>, x and z growing with world X and Z)
/// and centred on the map. A non-zero entry marks an unlocked tile; its value is the order in which the
/// tiles were unlocked. The vanilla grid covers the central 5 x 5 tiles; 81 Tiles 2 replaces it with a
/// 9 x 9 grid covering the whole map.
/// </remarks>
internal static class AreaGrid
{
    /// <summary>Converts the grid into tiles of the map's 9 x 9 grid, in grid order.</summary>
    /// <param name="grid">A copy of <c>GameAreaManager.m_areaGrid</c>, or <see langword="null"/>.</param>
    /// <param name="problem">Why no tiles could be read, or <see langword="null"/>.</param>
    /// <returns>The unlocked tiles; empty when there is a problem.</returns>
    public static IList<TileCoordinate> ToTiles(int[]? grid, out string? problem)
    {
        var tiles = new List<TileCoordinate>();
        if (grid is null)
        {
            problem = "The game has no grid of unlocked tiles (GameAreaManager.m_areaGrid is null).";
            return tiles;
        }

        int size = GetSize(grid.Length);
        if (size == 0)
        {
            problem = string.Format(
                CultureInfo.InvariantCulture,
                "The grid of unlocked tiles has {0} entries, which isn't a centred square grid of at most {1} x {1} tiles; perhaps a mod changed it. The unlocked tiles aren't recorded.",
                grid.Length,
                TileCoordinate.GridSize);
            return tiles;
        }

        int offset = (TileCoordinate.GridSize - size) / 2;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                if (grid[(z * size) + x] != 0)
                {
                    tiles.Add(new TileCoordinate(x + offset, z + offset));
                }
            }
        }

        problem = null;
        return tiles;
    }

    // An odd size from 1 to 9 can be centred on the 9 x 9 map; 0 means the length fits none.
    private static int GetSize(int length)
    {
        int size = (int)Math.Round(Math.Sqrt(length));
        return size * size == length && size % 2 == 1 && size <= TileCoordinate.GridSize ? size : 0;
    }
}
