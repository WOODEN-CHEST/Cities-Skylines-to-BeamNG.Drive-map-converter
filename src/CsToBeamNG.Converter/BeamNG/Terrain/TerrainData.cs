using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// The content of a <c>.ter</c> file: a stored height and a material index for each of Size × Size samples, and
/// the names of the materials (docs/research/beamng-level-format.md, Terrain).
/// </summary>
/// <remarks>
/// Samples are stored row by row: index = y × <see cref="Size"/> + x. Sample (0, 0) lies at the
/// <c>TerrainBlock</c>'s position, its lower-left corner; x grows towards +X and y towards +Y, one
/// <c>squareSize</c> per step.
/// </remarks>
public sealed class TerrainData
{
    /// <summary>The largest size the game is known to load (docs/research/beamng-level-format.md, Sizes).</summary>
    public const int MaxSize = 8192;

    /// <summary>The material index that makes a hole: no rendering and no collision.</summary>
    public const byte HoleMaterialIndex = 255;

    /// <summary>The most materials a terrain can have; index 255 is reserved for holes.</summary>
    public const int MaxMaterialCount = 254;

    /// <summary>The longest material name the file format can store (a length byte).</summary>
    public const int MaxMaterialNameLength = byte.MaxValue;

    private readonly ushort[] _heights;
    private readonly byte[] _materials;
    private readonly string[] _materialNames;

    /// <summary>Creates a terrain with all heights 0 and all samples of material 0.</summary>
    /// <param name="size">The samples per side: a power of two up to <see cref="MaxSize"/>.</param>
    /// <param name="materialNames">
    /// The materials' <c>internalName</c>s, which the terrain materials define. Printable ASCII, at least one, at
    /// most <see cref="MaxMaterialCount"/>, without duplicates.
    /// </param>
    public TerrainData(int size, IEnumerable<string> materialNames)
    {
        ArgumentNullException.ThrowIfNull(materialNames);
        if (size < 2 || size > MaxSize || !BitOperations.IsPow2(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, $"The size must be a power of two from 2 to {MaxSize}.");
        }

        _materialNames = [.. materialNames];
        ValidateMaterialNames(_materialNames);
        Size = size;
        _heights = new ushort[size * size];
        _materials = new byte[size * size];
    }

    /// <summary>The samples per side.</summary>
    public int Size { get; }

    /// <summary>The materials' names; a sample's material index points into this list.</summary>
    public IReadOnlyList<string> MaterialNames => _materialNames;

    /// <summary>The stored heights, row by row (see the remarks on <see cref="TerrainData"/>).</summary>
    public Span<ushort> Heights => _heights;

    /// <summary>The material indices, row by row; <see cref="HoleMaterialIndex"/> makes a hole.</summary>
    public Span<byte> Materials => _materials;

    /// <summary>The stored height of a sample.</summary>
    /// <param name="x">The column, towards +X.</param>
    /// <param name="y">The row, towards +Y.</param>
    /// <returns>The stored height.</returns>
    public ushort GetHeight(int x, int y) => _heights[IndexOf(x, y)];

    /// <summary>Sets the stored height of a sample.</summary>
    /// <param name="x">The column, towards +X.</param>
    /// <param name="y">The row, towards +Y.</param>
    /// <param name="height">The stored height; see <see cref="TerrainHeightScale"/>.</param>
    public void SetHeight(int x, int y, ushort height) => _heights[IndexOf(x, y)] = height;

    /// <summary>The material index of a sample.</summary>
    /// <param name="x">The column, towards +X.</param>
    /// <param name="y">The row, towards +Y.</param>
    /// <returns>The index into <see cref="MaterialNames"/>, or <see cref="HoleMaterialIndex"/>.</returns>
    public byte GetMaterial(int x, int y) => _materials[IndexOf(x, y)];

    /// <summary>Sets the material index of a sample.</summary>
    /// <param name="x">The column, towards +X.</param>
    /// <param name="y">The row, towards +Y.</param>
    /// <param name="material">An index into <see cref="MaterialNames"/>, or <see cref="HoleMaterialIndex"/>.</param>
    public void SetMaterial(int x, int y, byte material)
    {
        if (material >= _materialNames.Length && material != HoleMaterialIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(material), material, $"The terrain has {_materialNames.Length} materials.");
        }

        _materials[IndexOf(x, y)] = material;
    }

    internal static void ValidateMaterialNames(IReadOnlyList<string> names)
    {
        if (names.Count is < 1 or > MaxMaterialCount)
        {
            throw new ArgumentException($"A terrain needs 1 to {MaxMaterialCount} materials, not {names.Count}.", nameof(names));
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxMaterialNameLength || !IsPrintableAscii(name))
            {
                throw new ArgumentException(
                    $"Terrain material names are 1 to {MaxMaterialNameLength} printable ASCII characters: '{name}'.", nameof(names));
            }

            if (!unique.Add(name))
            {
                throw new ArgumentException($"The terrain material '{name}' is listed twice.", nameof(names));
            }
        }
    }

    internal static byte[] EncodeMaterialName(string name) => Encoding.ASCII.GetBytes(name);

    private static bool IsPrintableAscii(string text)
    {
        foreach (char c in text)
        {
            if (c is < ' ' or > '~')
            {
                return false;
            }
        }

        return true;
    }

    private int IndexOf(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Size);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Size);
        return (y * Size) + x;
    }
}
