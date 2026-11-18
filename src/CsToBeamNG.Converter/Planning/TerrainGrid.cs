using System;
using System.Globalization;
using System.Numerics;
using CsToBeamNG.Converter.Coordinates;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Planning;

/// <summary>
/// The level's terrain grid: a square TerrainBlock of <see cref="Size"/> × <see cref="Size"/> samples,
/// <see cref="SquareSize"/> metres apart, placed over the part of the city it shows (docs/architecture.md, Scale
/// strategy; D36). It relates BeamNG's samples to positions in the city.
/// </summary>
/// <remarks>
/// <para>
/// BeamNG stores a terrain's samples row by row from its lower-left corner, the <c>TerrainBlock</c>'s position: sample
/// (column, row) lies at that position plus (column, row) × square size in BeamNG's X and Y
/// (docs/research/beamng-level-format.md, Terrain). Which city direction each of these axes is, the grid asks
/// <see cref="CoordinateMapping"/>, so the terrain follows any correction of the mapping.
/// </para>
/// <para>
/// The samples are placed on the lattice of the game's detail heights, so with square sizes that divide its spacing
/// every lattice point is a sample and the game's heights carry over exactly.
/// </para>
/// </remarks>
public sealed class TerrainGrid
{
    // The city position of sample (0, 0), and the city X and Z steps of one column and one row, per metre.
    private readonly double _originX;
    private readonly double _originZ;
    private readonly int _columnX;
    private readonly int _columnZ;
    private readonly int _rowX;
    private readonly int _rowZ;

    /// <summary>Creates a grid from the city corner it starts at.</summary>
    /// <param name="size">The samples per side, 2 or more.</param>
    /// <param name="squareSize">The distance between samples, in metres.</param>
    /// <param name="minX">The smallest city X the samples cover.</param>
    /// <param name="minZ">The smallest city Z the samples cover.</param>
    /// <param name="area">The area the grid was planned for.</param>
    /// <exception cref="InvalidOperationException">
    /// <see cref="CoordinateMapping"/> doesn't map the city's ground plane onto BeamNG's ground plane axis by axis.
    /// </exception>
    public TerrainGrid(int size, float squareSize, double minX, double minZ, MapRect area)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 2);
        if (!(squareSize > 0) || !float.IsFinite(squareSize))
        {
            throw new ArgumentOutOfRangeException(nameof(squareSize), squareSize, "The square size must be positive.");
        }

        Size = size;
        SquareSize = squareSize;
        Area = area;
        double extent = ExtentMeters;
        Coverage = new MapRect((float)minX, (float)minZ, (float)(minX + extent), (float)(minZ + extent));
        CoversArea = Coverage.MinX <= area.MinX && Coverage.MinZ <= area.MinZ && Coverage.MaxX >= area.MaxX && Coverage.MaxZ >= area.MaxZ;

        // The covered square's corners in BeamNG; the TerrainBlock starts at the smallest x and y.
        Vector3 first = CoordinateMapping.ToBeamNG(new Vector3((float)minX, 0, (float)minZ));
        Vector3 second = CoordinateMapping.ToBeamNG(new Vector3((float)(minX + extent), 0, (float)(minZ + extent)));
        BeamNGPosition = new Vector2(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y));

        // The city directions of BeamNG's +X and +Y; each must be one of the city's horizontal axes.
        Vector3 origin = CoordinateMapping.ToCitiesSkylines(new Vector3(BeamNGPosition, 0));
        Vector3 column = CoordinateMapping.ToCitiesSkylines(new Vector3(BeamNGPosition.X + 1, BeamNGPosition.Y, 0)) - origin;
        Vector3 row = CoordinateMapping.ToCitiesSkylines(new Vector3(BeamNGPosition.X, BeamNGPosition.Y + 1, 0)) - origin;
        Vector3 up = CoordinateMapping.ToBeamNG(Vector3.UnitY) - CoordinateMapping.ToBeamNG(Vector3.Zero);
        if (!IsAxis(column) || !IsAxis(row) || Math.Abs((int)column.X) == Math.Abs((int)row.X) || up != Vector3.UnitZ)
        {
            throw new InvalidOperationException("The coordinate mapping doesn't map the city's ground onto BeamNG's ground axis by axis.");
        }

        _columnX = (int)column.X;
        _columnZ = (int)column.Z;
        _rowX = (int)row.X;
        _rowZ = (int)row.Z;
        _originX = origin.X;
        _originZ = origin.Z;
    }

    /// <summary>The samples per side.</summary>
    public int Size { get; }

    /// <summary>The distance between neighbouring samples, in metres.</summary>
    public float SquareSize { get; }

    /// <summary>The distance from the first to the last sample of a row, in metres.</summary>
    public double ExtentMeters => (Size - 1) * (double)SquareSize;

    /// <summary>The city area the samples cover, from the first to the last sample.</summary>
    public MapRect Coverage { get; }

    /// <summary>The area the grid was planned for.</summary>
    public MapRect Area { get; }

    /// <summary>Whether <see cref="Coverage"/> contains all of <see cref="Area"/>; false if the area was too large.</summary>
    public bool CoversArea { get; }

    /// <summary>The <c>TerrainBlock</c>'s lower-left corner in BeamNG's X and Y: the position of sample (0, 0).</summary>
    public Vector2 BeamNGPosition { get; }

    /// <summary>
    /// Plans a grid that covers an area: the finest of <see cref="TerrainGridOptions.SquareSizes"/> for which a terrain of
    /// at most <see cref="TerrainGridOptions.MaxSize"/> samples covers it, with the smallest such size. The grid is
    /// centred on the area as far as the lattice allows, and kept inside the map where it fits.
    /// </summary>
    /// <param name="area">The area to cover, in city coordinates.</param>
    /// <param name="lattice">The game's detail lattice, whose points the samples are placed on.</param>
    /// <param name="map">The map's extent; the grid stays inside it when it can.</param>
    /// <param name="options">Options, or <see langword="null"/> for <see cref="TerrainGridOptions.Default"/>.</param>
    /// <returns>
    /// The grid. If no allowed size covers the area, the largest grid of the coarsest square size, centred on it, with
    /// <see cref="CoversArea"/> false.
    /// </returns>
    public static TerrainGrid Plan(MapRect area, GridGeometry lattice, MapRect map, TerrainGridOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(lattice);
        options ??= TerrainGridOptions.Default;
        if (options.MinSize > options.MaxSize)
        {
            throw new ArgumentException("The smallest size exceeds the largest.", nameof(options));
        }

        double width = Math.Max(area.Width, area.Depth);
        foreach (float squareSize in options.SquareSizes)
        {
            for (int size = options.MinSize; size <= options.MaxSize; size *= 2)
            {
                double extent = (size - 1) * (double)squareSize;
                if (extent < width)
                {
                    continue;
                }

                if (TryPlace(area.MinX, area.MaxX, extent, lattice.OriginX, lattice.Spacing, map.MinX, map.MaxX, out double minX)
                    && TryPlace(area.MinZ, area.MaxZ, extent, lattice.OriginZ, lattice.Spacing, map.MinZ, map.MaxZ, out double minZ))
                {
                    return new TerrainGrid(size, squareSize, minX, minZ, area);
                }
            }
        }

        float coarsest = options.SquareSizes[^1];
        double largest = (options.MaxSize - 1) * (double)coarsest;
        return new TerrainGrid(
            options.MaxSize,
            coarsest,
            Snap(((area.MinX + area.MaxX) / 2.0) - (largest / 2), lattice.OriginX, lattice.Spacing),
            Snap(((area.MinZ + area.MaxZ) / 2.0) - (largest / 2), lattice.OriginZ, lattice.Spacing),
            area);
    }

    /// <summary>Returns the city position of a sample.</summary>
    /// <param name="column">The sample's column, towards BeamNG's +X; any value, also outside the grid.</param>
    /// <param name="row">The sample's row, towards BeamNG's +Y.</param>
    /// <returns>The city X and Z, in metres.</returns>
    public (double X, double Z) ToCitiesSkylines(double column, double row)
    {
        double s = SquareSize;
        return (_originX + (((column * _columnX) + (row * _rowX)) * s), _originZ + (((column * _columnZ) + (row * _rowZ)) * s));
    }

    /// <summary>Returns the fractional sample position of a city position.</summary>
    /// <param name="x">The city X, in metres.</param>
    /// <param name="z">The city Z, in metres.</param>
    /// <returns>The column and row; whole numbers at samples, outside 0 to <see cref="Size"/> − 1 beyond the grid.</returns>
    public (double Column, double Row) ToSample(double x, double z)
    {
        double dx = x - _originX;
        double dz = z - _originZ;
        double s = SquareSize;
        return (((dx * _columnX) + (dz * _columnZ)) / s, ((dx * _rowX) + (dz * _rowZ)) / s);
    }

    /// <summary>Returns the BeamNG position of a sample at a height.</summary>
    /// <param name="column">The sample's column.</param>
    /// <param name="row">The sample's row.</param>
    /// <param name="height">The height, in metres; BeamNG's z.</param>
    public Vector3 ToBeamNG(double column, double row, double height)
    {
        (double x, double z) = ToCitiesSkylines(column, row);
        return CoordinateMapping.ToBeamNG(new Vector3((float)x, (float)height, (float)z));
    }

    /// <summary>Returns the samples that lie in a city area, clamped to the grid.</summary>
    /// <param name="area">The area, in city coordinates.</param>
    /// <param name="firstColumn">The first column.</param>
    /// <param name="lastColumn">The last column.</param>
    /// <param name="firstRow">The first row.</param>
    /// <param name="lastRow">The last row.</param>
    /// <returns>Whether any sample lies in the area.</returns>
    public bool TryGetSampleRange(MapRect area, out int firstColumn, out int lastColumn, out int firstRow, out int lastRow)
    {
        (double c0, double r0) = ToSample(area.MinX, area.MinZ);
        (double c1, double r1) = ToSample(area.MaxX, area.MaxZ);
        firstColumn = (int)Math.Max(0, Math.Ceiling(Math.Min(c0, c1) - 1e-9));
        lastColumn = (int)Math.Min(Size - 1, Math.Floor(Math.Max(c0, c1) + 1e-9));
        firstRow = (int)Math.Max(0, Math.Ceiling(Math.Min(r0, r1) - 1e-9));
        lastRow = (int)Math.Min(Size - 1, Math.Floor(Math.Max(r0, r1) + 1e-9));
        return firstColumn <= lastColumn && firstRow <= lastRow;
    }

    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Size} × {Size} samples, {SquareSize:0.###} m apart, covering {Coverage}");

    private static bool IsAxis(Vector3 direction) =>
        direction.Y == 0 && ((Math.Abs(direction.X) == 1 && direction.Z == 0) || (direction.X == 0 && Math.Abs(direction.Z) == 1));

    // Finds the start of a span of the given extent that contains [min, max], on the lattice, as close to centred as
    // possible, and inside the map's [mapMin, mapMax] if that is possible too.
    private static bool TryPlace(double min, double max, double extent, double latticeOrigin, double spacing, double mapMin, double mapMax, out double start)
    {
        double low = max - extent;
        double high = min;
        double centred = ((min + max) / 2) - (extent / 2);
        if (TryPlaceBetween(Math.Max(low, mapMin), Math.Min(high, mapMax - extent), centred, latticeOrigin, spacing, out start))
        {
            return true;
        }

        return TryPlaceBetween(low, high, centred, latticeOrigin, spacing, out start);
    }

    private static bool TryPlaceBetween(double low, double high, double preferred, double latticeOrigin, double spacing, out double start)
    {
        start = 0;
        if (low > high)
        {
            return false;
        }

        double firstStep = Math.Ceiling((low - latticeOrigin) / spacing);
        double lastStep = Math.Floor((high - latticeOrigin) / spacing);
        if (firstStep > lastStep)
        {
            return false;
        }

        double step = Math.Clamp(Math.Round((preferred - latticeOrigin) / spacing), firstStep, lastStep);
        start = latticeOrigin + (step * spacing);
        return true;
    }

    private static double Snap(double value, double latticeOrigin, double spacing) =>
        latticeOrigin + (Math.Round((value - latticeOrigin) / spacing) * spacing);
}
