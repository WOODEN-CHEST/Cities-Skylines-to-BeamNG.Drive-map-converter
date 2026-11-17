using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// A terrain material (<c>TerrainMaterial</c>): three texture layers that blend with distance, close-up
/// <see cref="Detail"/>, medium-scale <see cref="Macro"/> and far <see cref="Base"/>, each with up to five maps
/// (docs/research/beamng-level-format.md, Terrain materials).
/// </summary>
public sealed class TerrainMaterial
{
    private readonly string _internalName = string.Empty;

    /// <summary>The name that the <c>.ter</c> file's material list uses.</summary>
    public required string InternalName
    {
        get => _internalName;
        init
        {
            TerrainData.ValidateMaterialNames([value]);
            _internalName = value;
        }
    }

    /// <summary>The ground model that sets grip, sounds and particles, such as <c>GRASS</c> or <c>ASPHALT</c>.</summary>
    public required string GroundModelName { get; init; }

    /// <summary>The <c>annotation</c> that classifies the surface, such as <c>ASPHALT</c>, or <see langword="null"/>.</summary>
    public string? Annotation { get; init; }

    /// <summary>The far layer, shown at a distance.</summary>
    public required TerrainMaterialLayer Base { get; init; }

    /// <summary>The medium-scale layer, which breaks up repetition.</summary>
    public required TerrainMaterialLayer Macro { get; init; }

    /// <summary>The close-up layer.</summary>
    public required TerrainMaterialLayer Detail { get; init; }

    /// <summary>The <c>macroDistances</c> field: the distances over which the macro layer fades in and out.</summary>
    public Vector4? MacroDistances { get; init; }

    /// <summary>The <c>detailDistances</c> field: the distances over which the detail layer fades in and out.</summary>
    public Vector4? DetailDistances { get; init; }
}
