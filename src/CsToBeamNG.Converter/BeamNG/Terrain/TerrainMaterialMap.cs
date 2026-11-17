using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>One texture map of a <see cref="TerrainMaterialLayer"/>.</summary>
/// <param name="Texture">The texture. Reference the game's shared textures; never copy them.</param>
public sealed record TerrainMaterialMap(VirtualPath Texture)
{
    /// <summary>The <c>…Strength</c> field, a pair of strengths, or <see langword="null"/> for the game's default.</summary>
    public Vector2? Strength { get; init; }
}
