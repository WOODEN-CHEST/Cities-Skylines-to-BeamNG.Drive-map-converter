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

    /// <summary>Describes network prefabs: how many, and how many lanes they define.</summary>
    /// <param name="prefabs">The prefabs.</param>
    public static string Describe(NetPrefabsSectionContent prefabs)
    {
        int lanes = 0;
        foreach (NetPrefabRecord prefab in prefabs.Prefabs)
        {
            lanes += prefab.Lanes.Count;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} prefabs with {1} lanes", prefabs.Prefabs.Count, lanes);
    }

    /// <summary>Describes network nodes: how many of each kind, with traffic lights, at the map's edge and underground.</summary>
    /// <param name="nodes">The nodes.</param>
    public static string Describe(NetNodesSectionContent nodes)
    {
        int junctions = 0;
        int bends = 0;
        int ends = 0;
        int middles = 0;
        int trafficLights = 0;
        int outside = 0;
        int underground = 0;
        foreach (NetNodeRecord node in nodes.Nodes)
        {
            junctions += node.HasFlags(NetNodeRecord.JunctionFlag) ? 1 : 0;
            bends += node.HasFlags(NetNodeRecord.BendFlag) ? 1 : 0;
            ends += node.HasFlags(NetNodeRecord.EndFlag) ? 1 : 0;
            middles += node.HasFlags(NetNodeRecord.MiddleFlag) ? 1 : 0;
            trafficLights += node.HasFlags(NetNodeRecord.TrafficLightsFlag) ? 1 : 0;
            outside += node.HasFlags(NetNodeRecord.OutsideFlag) ? 1 : 0;
            underground += node.HasFlags(NetNodeRecord.UndergroundFlag) ? 1 : 0;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} nodes: {1} junctions, {2} bends, {3} ends, {4} middle nodes; {5} with traffic lights, {6} outside connections, {7} underground",
            nodes.Nodes.Count,
            junctions,
            bends,
            ends,
            middles,
            trafficLights,
            outside,
            underground);
    }

    /// <summary>Describes network segments: how many, and their total length by the game's average lane lengths.</summary>
    /// <param name="segments">The segments.</param>
    public static string Describe(NetSegmentsSectionContent segments)
    {
        double meters = 0;
        foreach (NetSegmentRecord segment in segments.Segments)
        {
            meters += segment.AverageLength;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} segments, {1} km long by their average lane lengths",
            segments.Segments.Count,
            (meters / 1000).ToString("0.###", CultureInfo.InvariantCulture));
    }

    /// <summary>Describes network lanes: how many, and their total length.</summary>
    /// <param name="lanes">The lanes.</param>
    public static string Describe(NetLanesSectionContent lanes)
    {
        double meters = 0;
        foreach (NetLaneRecord lane in lanes.Lanes)
        {
            meters += lane.Length;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} lanes, {1} km long",
            lanes.Lanes.Count,
            (meters / 1000).ToString("0.###", CultureInfo.InvariantCulture));
    }

    /// <summary>Describes building prefabs: how many, and how many of them have sub-buildings.</summary>
    /// <param name="prefabs">The prefabs.</param>
    public static string Describe(BuildingPrefabsSectionContent prefabs)
    {
        int withSubBuildings = 0;
        foreach (BuildingPrefabRecord prefab in prefabs.Prefabs)
        {
            withSubBuildings += prefab.SubBuildings.Count > 0 ? 1 : 0;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} prefabs, {1} with sub-buildings", prefabs.Prefabs.Count, withSubBuildings);
    }

    /// <summary>Describes buildings: how many, how many are sub-buildings, and how many are abandoned, collapsed or not drawn.</summary>
    /// <param name="buildings">The buildings.</param>
    public static string Describe(BuildingsSectionContent buildings)
    {
        int subBuildings = 0;
        int abandoned = 0;
        int collapsed = 0;
        int notDrawn = 0;
        foreach (BuildingRecord building in buildings.Buildings)
        {
            subBuildings += building.ParentBuilding != 0 ? 1 : 0;
            abandoned += building.HasFlags(BuildingRecord.AbandonedFlag) ? 1 : 0;
            collapsed += building.HasFlags(BuildingRecord.CollapsedFlag) ? 1 : 0;
            notDrawn += building.IsDrawn ? 0 : 1;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} buildings: {1} sub-buildings, {2} abandoned, {3} collapsed, {4} not drawn",
            buildings.Buildings.Count,
            subBuildings,
            abandoned,
            collapsed,
            notDrawn);
    }

    /// <summary>Describes prop prefabs: how many, and how many are decals, markers or have lights.</summary>
    /// <param name="prefabs">The prefabs.</param>
    public static string Describe(PropPrefabsSectionContent prefabs)
    {
        int decals = 0;
        int markers = 0;
        int lights = 0;
        foreach (PropPrefabRecord prefab in prefabs.Prefabs)
        {
            decals += prefab.Decal ? 1 : 0;
            markers += prefab.Marker ? 1 : 0;
            lights += prefab.LightEffects ? 1 : 0;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} prefabs: {1} decals, {2} markers, {3} with lights",
            prefabs.Prefabs.Count,
            decals,
            markers,
            lights);
    }

    /// <summary>Describes props: how many, how many keep their height, and how many the game doesn't draw.</summary>
    /// <param name="props">The props.</param>
    public static string Describe(PropsSectionContent props)
    {
        int fixedHeight = 0;
        int notDrawn = 0;
        foreach (PropRecord prop in props.Props)
        {
            fixedHeight += prop.HasFlags(PropRecord.FixedHeightFlag) ? 1 : 0;
            notDrawn += prop.IsDrawn ? 0 : 1;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} props: {1} with a fixed height, {2} not drawn", props.Props.Count, fixedHeight, notDrawn);
    }

    /// <summary>Describes tree prefabs: how many.</summary>
    /// <param name="prefabs">The prefabs.</param>
    public static string Describe(TreePrefabsSectionContent prefabs) =>
        string.Format(CultureInfo.InvariantCulture, "{0} prefabs", prefabs.Prefabs.Count);

    /// <summary>Describes trees: how many, how many the game doesn't draw, and the range of their scales.</summary>
    /// <param name="trees">The trees.</param>
    public static string Describe(TreesSectionContent trees)
    {
        int notDrawn = 0;
        float minScale = float.PositiveInfinity;
        float maxScale = float.NegativeInfinity;
        foreach (TreeRecord tree in trees.Trees)
        {
            notDrawn += tree.IsDrawn ? 0 : 1;
            minScale = tree.Scale < minScale ? tree.Scale : minScale;
            maxScale = tree.Scale > maxScale ? tree.Scale : maxScale;
        }

        return trees.Trees.Count == 0
            ? "0 trees"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0} trees: {1} not drawn; scales {2} to {3}",
                trees.Trees.Count,
                notDrawn,
                minScale.ToString("0.###", CultureInfo.InvariantCulture),
                maxScale.ToString("0.###", CultureInfo.InvariantCulture));
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
