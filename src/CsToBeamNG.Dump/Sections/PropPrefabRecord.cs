namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A prop prefab (<c>PropInfo</c>): its mesh bounds, scale range, colour variations and how the game draws it, as the
/// game stores them (docs/dump-format.md, <c>prop-prefabs</c>; docs/research/cs1-assets.md, Prefabs).
/// </summary>
/// <remarks>
/// Meshes and materials aren't part of version 0.1. Later versions add them as optional JSON properties, such as
/// references inside <see cref="Mesh"/>, which readers of 0.1 ignore.
/// </remarks>
public sealed class PropPrefabRecord
{
    /// <summary>The number of colour variations a prefab has.</summary>
    public const int ColorCount = 4;

    /// <summary>The prefab's index in the game's prefab collection, which props refer to (<c>m_infoIndex</c>).</summary>
    public uint Index { get; set; }

    /// <summary>The prefab's name, such as <c>Bench 01</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The prefab's item class (<c>m_class</c>).</summary>
    public DumpItemClass Class { get; set; } = new();

    /// <summary>The bounds of the main mesh (<c>m_mesh</c>) in the prefab's own coordinates; <see langword="null"/> if it has none.</summary>
    public DumpBounds? Mesh { get; set; }

    /// <summary>The bounds of the level-of-detail mesh (<c>m_lodMesh</c>); <see langword="null"/> if it has none.</summary>
    public DumpBounds? LodMesh { get; set; }

    /// <summary>The smallest scale the game draws a prop of this prefab with (<c>m_minScale</c>).</summary>
    public float MinScale { get; set; }

    /// <summary>The largest scale (<c>m_maxScale</c>).</summary>
    public float MaxScale { get; set; }

    /// <summary>Whether the prop is a decal, which the game draws onto the terrain (<c>m_isDecal</c>).</summary>
    public bool Decal { get; set; }

    /// <summary>Whether the prop is a marker, which the game draws only in the editors (<c>m_isMarker</c>).</summary>
    public bool Marker { get; set; }

    /// <summary>Whether the game drapes the mesh over the terrain (<c>m_requireHeightMap</c>).</summary>
    public bool RequireHeightMap { get; set; }

    /// <summary>Whether the prop floats on the water: the game draws it at the water's surface (<c>m_requireWaterMap</c>).</summary>
    public bool RequireWaterMap { get; set; }

    /// <summary>Whether the prop paints ruined ground onto the terrain (<c>m_createRuining</c>).</summary>
    public bool CreateRuining { get; set; }

    /// <summary>Whether the prop has a light among its effects (<c>m_effects</c>), such as a street lamp.</summary>
    public bool LightEffects { get; set; }

    /// <summary>Whether the prefab asks for colour variations (<c>m_useColorVariations</c>).</summary>
    public bool UseColorVariations { get; set; }

    /// <summary>The prefab's colour variations, <c>m_color0</c> to <c>m_color3</c>.</summary>
    public DumpColor[] Colors { get; } = new DumpColor[ColorCount];
}
