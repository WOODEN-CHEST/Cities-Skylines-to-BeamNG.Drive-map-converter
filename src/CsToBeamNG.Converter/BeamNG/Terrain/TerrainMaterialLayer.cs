namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>One texture layer of a <see cref="TerrainMaterial"/>.</summary>
public sealed record TerrainMaterialLayer
{
    /// <summary>
    /// The size in metres that one repeat of the textures covers, written as each map's <c>…TexSize</c> field, or
    /// <see langword="null"/> to leave it to the game.
    /// </summary>
    public float? TextureSize { get; init; }

    /// <summary>The colour map (<c>baseColor…Tex</c>).</summary>
    public TerrainMaterialMap? BaseColor { get; init; }

    /// <summary>The normal map (<c>normal…Tex</c>).</summary>
    public TerrainMaterialMap? Normal { get; init; }

    /// <summary>The roughness map (<c>roughness…Tex</c>).</summary>
    public TerrainMaterialMap? Roughness { get; init; }

    /// <summary>The ambient occlusion map (<c>ao…Tex</c>).</summary>
    public TerrainMaterialMap? AmbientOcclusion { get; init; }

    /// <summary>The height map used for blending (<c>height…Tex</c>).</summary>
    public TerrainMaterialMap? Height { get; init; }
}
