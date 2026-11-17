namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A tree prefab (<c>TreeInfo</c>): its mesh bounds and the ranges of scale and brightness the game draws its trees
/// with, as the game stores them (docs/dump-format.md, <c>tree-prefabs</c>; docs/research/cs1-assets.md, Prefabs).
/// </summary>
/// <remarks>
/// The game draws far trees as billboards it generates at runtime, so tree prefabs have no level-of-detail mesh of
/// their own. Meshes and materials aren't part of version 0.1; later versions add them as optional JSON properties,
/// which readers of 0.1 ignore.
/// </remarks>
public sealed class TreePrefabRecord
{
    /// <summary>The prefab's index in the game's prefab collection, which trees refer to (<c>m_infoIndex</c>).</summary>
    public uint Index { get; set; }

    /// <summary>The prefab's name, such as <c>Tree2variant</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The prefab's item class (<c>m_class</c>).</summary>
    public DumpItemClass Class { get; set; } = new();

    /// <summary>The bounds of the mesh (<c>m_mesh</c>) in the prefab's own coordinates; <see langword="null"/> if it has none.</summary>
    public DumpBounds? Mesh { get; set; }

    /// <summary>The smallest scale the game draws a tree of this prefab with (<c>m_minScale</c>).</summary>
    public float MinScale { get; set; }

    /// <summary>The largest scale (<c>m_maxScale</c>).</summary>
    public float MaxScale { get; set; }

    /// <summary>The smallest brightness (<c>m_minBrightness</c>).</summary>
    public float MinBrightness { get; set; }

    /// <summary>The largest brightness (<c>m_maxBrightness</c>).</summary>
    public float MaxBrightness { get; set; }

    /// <summary>The colour the game multiplies by a tree's brightness to tint it (<c>m_defaultColor</c>).</summary>
    public DumpColor DefaultColor { get; set; }

    /// <summary>Whether the tree paints ruined ground onto the terrain (<c>m_createRuining</c>).</summary>
    public bool CreateRuining { get; set; }
}
