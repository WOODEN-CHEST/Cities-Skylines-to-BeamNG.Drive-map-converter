using System.Globalization;
using System.Text;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Exporter.Core;

/// <summary>One-line descriptions of exported sections for <c>export.log</c>.</summary>
internal static class SectionSummary
{
    /// <summary>Describes a terrain: its grid, the height ranges and the tiles with detail heights.</summary>
    /// <param name="terrain">The terrain.</param>
    public static string Describe(TerrainSectionContent terrain)
    {
        var text = new StringBuilder();
        text.Append(terrain.Grid.Resolution.ToString(CultureInfo.InvariantCulture))
            .Append(" × ")
            .Append(terrain.Grid.Resolution.ToString(CultureInfo.InvariantCulture))
            .Append(" heights, raw ")
            .Append(Range(terrain.RawHeights, TerrainSection.MetersPerHeightUnit))
            .Append(", final ")
            .Append(Range(terrain.FinalHeights, TerrainSection.MetersPerHeightUnit))
            .Append("; detail heights for ");
        if (terrain.DetailTiles.Count == 0)
        {
            text.Append("no tiles");
        }

        for (int i = 0; i < terrain.DetailTiles.Count; i++)
        {
            text.Append(i == 0 ? "tiles " : ", ").Append(terrain.DetailTiles[i].Tile.ToString());
        }

        return text.ToString();
    }

    /// <summary>Describes the water: the sea level, how many cells hold water, and the sources.</summary>
    /// <param name="water">The water.</param>
    public static string Describe(WaterSectionContent water)
    {
        int wet = 0;
        foreach (ushort depth in water.Depth)
        {
            if (depth > 0)
            {
                wet++;
            }
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "sea level {0} m; {1} of {2} cells hold water; {3} water sources",
            water.SeaLevel.ToString("0.###", CultureInfo.InvariantCulture),
            wet,
            water.Depth.Length,
            water.Sources.Count);
    }

    private static string Range(ushort[] heights, float metersPerUnit)
    {
        if (heights.Length == 0)
        {
            return "none";
        }

        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        foreach (ushort height in heights)
        {
            min = height < min ? height : min;
            max = height > max ? height : max;
        }

        return (min * metersPerUnit).ToString("0.###", CultureInfo.InvariantCulture)
            + " to "
            + (max * metersPerUnit).ToString("0.###", CultureInfo.InvariantCulture)
            + " m";
    }
}
