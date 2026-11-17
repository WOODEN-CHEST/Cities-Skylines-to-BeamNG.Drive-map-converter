using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// The texture arrays of a level's terrain materials (<c>TerrainMaterialTextureSet</c>). The textures of each layer
/// must all have this size.
/// </summary>
public sealed record TerrainMaterialTextureSet
{
    /// <summary>Creates a texture set.</summary>
    /// <param name="name">The name that the <c>TerrainBlock</c> refers to.</param>
    /// <param name="baseTexSize">The pixel size of the base textures.</param>
    /// <param name="macroTexSize">The pixel size of the macro textures.</param>
    /// <param name="detailTexSize">The pixel size of the detail textures.</param>
    public TerrainMaterialTextureSet(string name, int baseTexSize, int macroTexSize, int detailTexSize)
    {
        BeamNGNames.ThrowIfNotSceneName(name, nameof(name));
        Name = name;
        BaseTexSize = ValidateSize(baseTexSize, nameof(baseTexSize));
        MacroTexSize = ValidateSize(macroTexSize, nameof(macroTexSize));
        DetailTexSize = ValidateSize(detailTexSize, nameof(detailTexSize));
    }

    /// <summary>The name that the <c>TerrainBlock</c> refers to.</summary>
    public string Name { get; }

    /// <summary>The pixel size (width and height) of the base textures.</summary>
    public int BaseTexSize { get; }

    /// <summary>The pixel size of the macro textures.</summary>
    public int MacroTexSize { get; }

    /// <summary>The pixel size of the detail textures.</summary>
    public int DetailTexSize { get; }

    private static int ValidateSize(int size, string parameterName) =>
        size > 0 && BitOperations.IsPow2(size)
            ? size
            : throw new ArgumentOutOfRangeException(parameterName, size, "Texture sizes are powers of two.");
}
