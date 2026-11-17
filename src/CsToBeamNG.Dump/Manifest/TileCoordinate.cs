using System;
using System.Globalization;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>
/// A tile of the map's 9 x 9 grid of 1920 m tiles. Tile (x, z) covers world X from
/// -8640 + 1920 * x to -8640 + 1920 * (x + 1) metres, and the same for Z; the centre tile is (4, 4)
/// (docs/dump-format.md, <c>city</c>; docs/research/cs1-save-and-world.md, World geometry).
/// </summary>
public readonly struct TileCoordinate : IEquatable<TileCoordinate>
{
    /// <summary>The number of tiles along each axis of the map.</summary>
    public const int GridSize = 9;

    /// <summary>Creates a tile coordinate.</summary>
    /// <param name="x">The column, 0 to 8, increasing with world X.</param>
    /// <param name="z">The row, 0 to 8, increasing with world Z.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is outside the grid.</exception>
    public TileCoordinate(int x, int z)
    {
        if (!IsInGrid(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Tile coordinates are 0 to 8.");
        }

        if (!IsInGrid(z))
        {
            throw new ArgumentOutOfRangeException(nameof(z), z, "Tile coordinates are 0 to 8.");
        }

        X = x;
        Z = z;
    }

    /// <summary>The column, 0 to 8, increasing with world X.</summary>
    public int X { get; }

    /// <summary>The row, 0 to 8, increasing with world Z.</summary>
    public int Z { get; }

    /// <summary>Whether a coordinate is within the grid.</summary>
    /// <param name="coordinate">An x or z coordinate.</param>
    public static bool IsInGrid(int coordinate) => coordinate is >= 0 and < GridSize;

    /// <inheritdoc/>
    public bool Equals(TileCoordinate other) => X == other.X && Z == other.Z;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TileCoordinate other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (X * GridSize) + Z;

    /// <summary>Returns <c>(x, z)</c>.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X, Z);

    /// <summary>Whether two tiles are the same.</summary>
    /// <param name="left">A tile.</param>
    /// <param name="right">Another tile.</param>
    public static bool operator ==(TileCoordinate left, TileCoordinate right) => left.Equals(right);

    /// <summary>Whether two tiles differ.</summary>
    /// <param name="left">A tile.</param>
    /// <param name="right">Another tile.</param>
    public static bool operator !=(TileCoordinate left, TileCoordinate right) => !left.Equals(right);
}
