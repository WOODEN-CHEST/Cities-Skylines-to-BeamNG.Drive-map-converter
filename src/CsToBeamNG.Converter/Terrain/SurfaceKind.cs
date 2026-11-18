namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// The kinds of ground the level's terrain is painted with. Each becomes one BeamNG terrain material
/// (<c>BeamNG.Terrain.TerrainMaterialPresets</c>); <see cref="TerrainSurfaceRules"/> decides which goes where.
/// </summary>
public enum SurfaceKind : byte
{
    /// <summary>Grass: flat and gently sloping dry ground.</summary>
    Grass,

    /// <summary>Bare dirt: slopes too steep for grass.</summary>
    Dirt,

    /// <summary>Rock: cliffs and other steep slopes.</summary>
    Rock,

    /// <summary>Sand: the ground under water and the low shores next to it.</summary>
    Sand,

    /// <summary>Asphalt: paved ground. No rule paints it yet; road beds will (step 1.6).</summary>
    Asphalt,
}
