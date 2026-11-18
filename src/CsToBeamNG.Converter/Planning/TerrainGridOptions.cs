using System;
using System.Collections.Generic;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG.Terrain;

namespace CsToBeamNG.Converter.Planning;

/// <summary>Options for <see cref="TerrainGrid.Plan"/>.</summary>
public sealed record TerrainGridOptions
{
    /// <summary>The smallest terrain size BeamNG.drive's terrain import accepts (docs/research/beamng-level-format.md, Sizes).</summary>
    public const int SmallestSize = 128;

    private readonly int _maxSize = TerrainData.MaxSize;
    private readonly int _minSize = SmallestSize;
    private readonly IReadOnlyList<float> _squareSizes = [1, 2, 4];

    /// <summary>The defaults: up to 8192 samples per side, squares of 1, 2 or 4 m.</summary>
    public static TerrainGridOptions Default { get; } = new();

    /// <summary>The largest number of samples per side: a power of two up to <see cref="TerrainData.MaxSize"/>.</summary>
    public int MaxSize
    {
        get => _maxSize;
        init => _maxSize = IsValidSize(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxSize), value, $"The size must be a power of two from 2 to {TerrainData.MaxSize}.");
    }

    /// <summary>The smallest number of samples per side: a power of two, at most <see cref="MaxSize"/>.</summary>
    public int MinSize
    {
        get => _minSize;
        init => _minSize = IsValidSize(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MinSize), value, $"The size must be a power of two from 2 to {TerrainData.MaxSize}.");
    }

    /// <summary>
    /// The distances between samples to choose from, in metres, finest first. The planner takes the finest one whose
    /// terrain fits within <see cref="MaxSize"/>. Sizes that divide the game's 4 m detail spacing keep its heights exact.
    /// </summary>
    public IReadOnlyList<float> SquareSizes
    {
        get => _squareSizes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            float[] sizes = [.. value];
            if (sizes.Length == 0)
            {
                throw new ArgumentException("At least one square size is needed.", nameof(SquareSizes));
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                if (!(sizes[i] > 0) || !float.IsFinite(sizes[i]) || (i > 0 && !(sizes[i] > sizes[i - 1])))
                {
                    throw new ArgumentException("Square sizes must be positive, finite and in increasing order.", nameof(SquareSizes));
                }
            }

            _squareSizes = sizes;
        }
    }

    private static bool IsValidSize(int size) => size >= 2 && size <= TerrainData.MaxSize && BitOperations.IsPow2(size);
}
