using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Cli;

/// <summary>
/// Parses the map areas that commands take: blocks of tiles such as <c>3-5,4</c>, and render areas such as
/// <c>map</c> or <c>-500,-500,500,500</c>.
/// </summary>
internal static class MapAreaSyntax
{
    /// <summary>The tile syntax, for help texts and errors.</summary>
    public const string TileRule = "x,z with each a tile number from 0 to 8 or a range of them, such as 4,4 or 3-5,3-4; tile 4,4 is the map's centre";

    /// <summary>Parses a block of tiles: <c>x,z</c>, where each is a tile number or a range <c>first-last</c>.</summary>
    /// <param name="text">The text, such as <c>3-5,4</c>.</param>
    /// <param name="tiles">The tiles of the block, ordered by z, then x.</param>
    /// <param name="error">Why the text isn't a block of tiles.</param>
    public static bool TryParseTiles(
        string text,
        [NotNullWhen(true)] out IReadOnlyList<TileCoordinate>? tiles,
        [NotNullWhen(false)] out string? error)
    {
        tiles = null;
        string[] parts = text.Split(',');
        if (parts.Length != 2
            || !TryParseTileRange(parts[0], out int firstX, out int lastX)
            || !TryParseTileRange(parts[1], out int firstZ, out int lastZ))
        {
            error = $"'{text}' is not a block of tiles. Use {TileRule}.";
            return false;
        }

        var block = new List<TileCoordinate>();
        for (int z = firstZ; z <= lastZ; z++)
        {
            for (int x = firstX; x <= lastX; x++)
            {
                block.Add(new TileCoordinate(x, z));
            }
        }

        tiles = block;
        error = null;
        return true;
    }

    /// <summary>
    /// Parses a render area: <c>aoi</c>, <c>map</c>, a block of tiles, or <c>minX,minZ,maxX,maxZ</c> in metres in
    /// the game's coordinates.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="area">The area.</param>
    /// <param name="error">Why the text isn't an area.</param>
    public static bool TryParseArea(string text, [NotNullWhen(true)] out RenderArea? area, [NotNullWhen(false)] out string? error)
    {
        area = null;
        string trimmed = text.Trim();
        if (string.Equals(trimmed, "aoi", StringComparison.OrdinalIgnoreCase))
        {
            area = RenderArea.AreaOfInterest;
        }
        else if (string.Equals(trimmed, "map", StringComparison.OrdinalIgnoreCase))
        {
            area = RenderArea.Map;
        }
        else
        {
            string[] parts = trimmed.Split(',');
            if (parts.Length == 2 && TryParseTiles(trimmed, out IReadOnlyList<TileCoordinate>? tiles, out _))
            {
                area = new RenderArea(trimmed, "tiles " + trimmed, MapGeometry.TileBounds(tiles));
            }
            else if (parts.Length == 4 && TryParseRectangle(parts, out MapRect bounds))
            {
                area = new RenderArea(trimmed, bounds.ToString(), bounds);
            }
        }

        error = area is null
            ? $"'{text}' is not an area. Use aoi (the area of interest), map (the whole map), tiles as {TileRule}, or minX,minZ,maxX,maxZ in metres, such as -500,-500,500,500."
            : null;
        return area is not null;
    }

    private static bool TryParseTileRange(string text, out int first, out int last)
    {
        string[] ends = text.Split('-');
        last = 0;
        if (!TryParseTile(ends[0], out first) || ends.Length > 2)
        {
            return false;
        }

        if (ends.Length == 1)
        {
            last = first;
            return true;
        }

        return TryParseTile(ends[1], out last) && first <= last;
    }

    private static bool TryParseTile(string text, out int tile) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out tile) && TileCoordinate.IsInGrid(tile);

    private static bool TryParseRectangle(string[] parts, out MapRect bounds)
    {
        bounds = default;
        float[] values = new float[4];
        for (int i = 0; i < values.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !float.IsFinite(values[i]))
            {
                return false;
            }
        }

        if (!(values[0] < values[2] && values[1] < values[3]))
        {
            return false;
        }

        bounds = new MapRect(values[0], values[1], values[2], values[3]);
        return true;
    }
}
