using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A building prefab (<c>BuildingInfo</c>): what kind of building it is, its size, how it sits on the terrain, its colour
/// variations and its sub-buildings, as the game stores them (docs/dump-format.md, <c>building-prefabs</c>;
/// docs/research/cs1-assets.md, Prefabs).
/// </summary>
/// <remarks>
/// Meshes, materials, building props and sub-meshes aren't part of version 0.1. Later versions add them as optional
/// JSON properties, such as references inside <see cref="Mesh"/>, which readers of 0.1 ignore.
/// </remarks>
public sealed class BuildingPrefabRecord
{
    /// <summary>The number of colour variations a prefab has.</summary>
    public const int ColorCount = 4;

    /// <summary>The prefab's index in the game's prefab collection, which buildings refer to (<c>m_infoIndex</c>).</summary>
    public uint Index { get; set; }

    /// <summary>The prefab's name, such as <c>L1 2x2 Detached01</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The prefab's item class (<c>m_class</c>).</summary>
    public DumpItemClass Class { get; set; } = new();

    /// <summary>The full type name of the prefab's AI (<c>m_buildingAI</c>), such as <c>ResidentialBuildingAI</c>; empty if it has none.</summary>
    public string AiType { get; set; } = string.Empty;

    /// <summary>The prefab's width in cells of 8 m (<c>m_cellWidth</c>).</summary>
    public int CellWidth { get; set; }

    /// <summary>The prefab's length in cells of 8 m (<c>m_cellLength</c>).</summary>
    public int CellLength { get; set; }

    /// <summary>
    /// The building's size as the game uses it for drawing and collisions (<c>m_size</c>), in metres: twice the larger
    /// extent of its meshes to each side on X and Z, and their height.
    /// </summary>
    public DumpVector3 Size { get; set; }

    /// <summary>
    /// The extent of the main mesh in the prefab's own coordinates, which the game computes from its vertices
    /// (<c>m_generatedInfo.m_min</c> and <c>m_max</c>); <see langword="null"/> if the game has none.
    /// </summary>
    public DumpBounds? Mesh { get; set; }

    /// <summary>The bounds of the level-of-detail mesh (<c>m_lodMesh</c>); <see langword="null"/> if the prefab has none.</summary>
    public DumpBounds? LodMesh { get; set; }

    /// <summary>Where the building may be placed, as the game names it (<c>BuildingInfo.PlacementMode</c>), such as <c>Roadside</c>.</summary>
    public string PlacementMode { get; set; } = string.Empty;

    /// <summary>How the building uses zoned cells, as the game names it (<c>BuildingInfo.ZoningMode</c>), such as <c>Straight</c>.</summary>
    public string ZoningMode { get; set; } = string.Empty;

    /// <summary>Whether the building is round (<c>m_circular</c>).</summary>
    public bool Circular { get; set; }

    /// <summary>
    /// Whether a lot deeper than the prefab's cells grows in front of the building (<c>m_expandFrontYard</c>); otherwise
    /// it grows behind it.
    /// </summary>
    public bool ExpandFrontYard { get; set; }

    /// <summary>Whether the building levels the terrain under it (<c>m_flattenTerrain</c>).</summary>
    public bool FlattenTerrain { get; set; }

    /// <summary>Whether it levels its whole lot (<c>m_flattenFullArea</c>).</summary>
    public bool FlattenFullArea { get; set; }

    /// <summary>Whether it paints pavement over its whole lot (<c>m_fullPavement</c>).</summary>
    public bool FullPavement { get; set; }

    /// <summary>Whether it paints gravel over its whole lot (<c>m_fullGravel</c>).</summary>
    public bool FullGravel { get; set; }

    /// <summary>Whether it cuts the terrain where it would cover the building (<c>m_clipTerrain</c>).</summary>
    public bool ClipTerrain { get; set; }

    /// <summary>Whether the game drapes the main mesh over the terrain (<c>m_requireHeightMap</c>).</summary>
    public bool RequireHeightMap { get; set; }

    /// <summary>Whether the game picks one of <see cref="Colors"/> for each building (<c>m_useColorVariations</c>).</summary>
    public bool UseColorVariations { get; set; }

    /// <summary>The prefab's colour variations, <c>m_color0</c> to <c>m_color3</c>.</summary>
    public DumpColor[] Colors { get; } = new DumpColor[ColorCount];

    /// <summary>The sub-buildings the game creates with the building, in the prefab's order (<c>m_subBuildings</c>).</summary>
    public IList<BuildingSubBuilding> SubBuildings { get; } = new List<BuildingSubBuilding>();
}
