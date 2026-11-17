using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>The terrain: a heightmap file placed in the world (docs/research/beamng-level-format.md, Terrain).</summary>
public sealed class TerrainBlock : SceneObject
{
    private readonly float _squareSize;
    private readonly float _maxHeight;

    /// <inheritdoc/>
    public override string ClassName => "TerrainBlock";

    /// <summary>The <c>.ter</c> file.</summary>
    public required VirtualPath TerrainFile { get; init; }

    /// <summary>The lower-left corner (smallest x and y) of the terrain; z is the height of a stored height of 0.</summary>
    public required Vector3 Position { get; init; }

    /// <summary>The distance between neighbouring height samples, in metres.</summary>
    public required float SquareSize
    {
        get => _squareSize;
        init => _squareSize = value > 0 && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(SquareSize), value, "The square size must be positive.");
    }

    /// <summary>The height, above <see cref="Position"/>, of the largest stored height.</summary>
    public required float MaxHeight
    {
        get => _maxHeight;
        init => _maxHeight = value > 0 && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxHeight), value, "The maximum height must be positive.");
    }

    /// <summary>The name of the terrain's <c>TerrainMaterialTextureSet</c>.</summary>
    public required string MaterialTextureSet { get; init; }

    /// <summary>The resolution of the generated base texture, or <see langword="null"/> for the game's default.</summary>
    public int? BaseTexSize { get; init; }

    internal override Vector3? ScenePosition => Position;

    internal override void AddFields(SceneFields fields)
    {
        if (BaseTexSize is int baseTexSize)
        {
            fields.Set("baseTexSize", baseTexSize);
        }

        fields.Set("materialTextureSet", MaterialTextureSet);
        fields.Set("maxHeight", MaxHeight);
        fields.Set("squareSize", SquareSize);
        fields.Set("terrainFile", TerrainFile.Value);
    }
}
