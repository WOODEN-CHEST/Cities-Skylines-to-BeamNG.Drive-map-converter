using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>
/// A material for meshes (<c>Material</c>, version 1.5, physically based), as a <c>*.materials.json</c> file
/// defines it (docs/research/beamng-level-format.md, Materials). This first version has constant factors only;
/// textures come with converted assets.
/// </summary>
public sealed class ShapeMaterial
{
    private readonly string _name = string.Empty;

    /// <summary>
    /// The material's name, which is also its <c>mapTo</c>: the Collada material name it applies to. Material
    /// names are global in the game, so prefix them, for example with the level name.
    /// </summary>
    public required string Name
    {
        get => _name;
        init
        {
            BeamNGNames.ThrowIfNotSceneName(value, nameof(Name));
            _name = value;
        }
    }

    /// <summary>The colour and opacity (<c>baseColorFactor</c>), each from 0 to 1, or <see langword="null"/>.</summary>
    public Vector4? BaseColorFactor { get; init; }

    /// <summary>The roughness (<c>roughnessFactor</c>), from 0 (mirror) to 1, or <see langword="null"/>.</summary>
    public float? RoughnessFactor { get; init; }

    /// <summary>The metalness (<c>metallicFactor</c>), from 0 to 1, or <see langword="null"/>.</summary>
    public float? MetallicFactor { get; init; }
}
