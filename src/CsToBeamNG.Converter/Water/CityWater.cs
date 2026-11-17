using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Water;

/// <summary>
/// The city's water in the world model: the sea level, the water simulation's cells on the 16 m grid and the water
/// sources, in the game's units (docs/dump-format.md, <c>water</c>; docs/research/cs1-save-and-world.md, Water).
/// </summary>
public sealed class CityWater : ISectionData
{
    /// <summary>The height of one stored unit of ground and depth, in metres.</summary>
    public const double MetersPerUnit = WaterSection.MetersPerHeightUnit;

    // The area of one cell: a point of the grid stands for a square of one spacing.
    private readonly double _cellAreaSquareMeters;
    private readonly ushort[] _ground;
    private readonly ushort[] _depth;

    /// <summary>Creates the water from a section's content.</summary>
    /// <param name="content">The content, as <see cref="WaterSectionReader"/> read it. The water keeps its arrays.</param>
    /// <param name="sources">The usable water sources, ordered by id.</param>
    /// <exception cref="ArgumentException">The arrays don't match the grid.</exception>
    public CityWater(WaterSectionContent content, IReadOnlyList<WaterSourceRecord> sources)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sources);
        int points = content.Grid.PointCount;
        if (content.Ground.Length != points || content.Depth.Length != points)
        {
            throw new ArgumentException("The water cells don't match the grid.", nameof(content));
        }

        SeaLevel = content.SeaLevel;
        Grid = content.Grid;
        _ground = content.Ground;
        _depth = content.Depth;
        Sources = sources;
        _cellAreaSquareMeters = (double)Grid.Spacing * Grid.Spacing;
        ushort maxDepth = 0;
        int lowest = int.MaxValue;
        int highest = int.MinValue;
        for (int k = 0; k < points; k++)
        {
            if (_depth[k] > 0)
            {
                WetCells++;
                maxDepth = Math.Max(maxDepth, _depth[k]);
                int surface = _ground[k] + _depth[k];
                lowest = Math.Min(lowest, surface);
                highest = Math.Max(highest, surface);
            }
        }

        MaxDepthMeters = maxDepth * MetersPerUnit;
        LowestSurfaceMeters = WetCells == 0 ? 0 : lowest * MetersPerUnit;
        HighestSurfaceMeters = WetCells == 0 ? 0 : highest * MetersPerUnit;
    }

    /// <summary>The sea level, in metres: the water level the simulation keeps at the map's edges.</summary>
    public float SeaLevel { get; }

    /// <summary>Where the cells lie: one cell per point of this grid.</summary>
    public GridGeometry Grid { get; }

    /// <summary>Each cell's ground in 1/64 m, row by row: the bottom the water simulation fills.</summary>
    public ReadOnlySpan<ushort> Ground => _ground;

    /// <summary>Each cell's water depth above its ground in 1/64 m, row by row; 0 where it is dry.</summary>
    public ReadOnlySpan<ushort> Depth => _depth;

    /// <summary>The water sources, ordered by id.</summary>
    public IReadOnlyList<WaterSourceRecord> Sources { get; }

    /// <summary>The number of cells that hold water.</summary>
    public int WetCells { get; }

    /// <summary>The deepest water, in metres.</summary>
    public double MaxDepthMeters { get; }

    /// <summary>The lowest water surface of a wet cell, in metres; 0 if no cell is wet.</summary>
    public double LowestSurfaceMeters { get; }

    /// <summary>The highest water surface of a wet cell, in metres; 0 if no cell is wet.</summary>
    public double HighestSurfaceMeters { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("sea level", string.Create(CultureInfo.InvariantCulture, $"{SeaLevel:0.###} m")),
        new("grid", Grid.ToString()),
        new("cells with water", string.Create(
            CultureInfo.InvariantCulture,
            $"{WetCells} of {Grid.PointCount} ({WetCells * _cellAreaSquareMeters / 1e6:0.##} km²), up to {MaxDepthMeters:0.##} m deep")),
        new("water surface", WetCells == 0
            ? "none"
            : string.Create(CultureInfo.InvariantCulture, $"{LowestSurfaceMeters:0.##} to {HighestSurfaceMeters:0.##} m")),
        new("water sources", DescribeSources()),
    ];

    /// <inheritdoc/>
    /// <remarks>The water covers the whole map, so it doesn't bound the area of interest.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>
    /// Returns the water depth at a world position, in metres, interpolated bilinearly between the four surrounding
    /// cells; outside the grid, the nearest edge applies.
    /// </summary>
    /// <param name="x">The world X, in metres.</param>
    /// <param name="z">The world Z, in metres.</param>
    public double SampleDepth(double x, double z)
    {
        int last = Grid.Resolution - 1;
        double gridX = Math.Clamp(Finite((x - Grid.OriginX) / Grid.Spacing), 0, last);
        double gridZ = Math.Clamp(Finite((z - Grid.OriginZ) / Grid.Spacing), 0, last);
        int i = Math.Min((int)gridX, last - 1);
        int j = Math.Min((int)gridZ, last - 1);
        double tx = gridX - i;
        double tz = gridZ - j;
        int row = j * Grid.Resolution;
        double bottom = Lerp(_depth[row + i], _depth[row + i + 1], tx);
        double top = Lerp(_depth[row + Grid.Resolution + i], _depth[row + Grid.Resolution + i + 1], tx);
        return Lerp(bottom, top, tz) * MetersPerUnit;
    }

    /// <summary>
    /// Returns the water surface of a cell, in metres: its ground plus its depth, the height the game draws the water
    /// at before smoothing it; <see langword="null"/> where the cell is dry (docs/research/cs1-save-and-world.md, Water).
    /// </summary>
    /// <param name="i">The cell's column, along X.</param>
    /// <param name="j">The cell's row, along Z.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the grid.</exception>
    public double? SurfaceAt(int i, int j)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)i, (uint)Grid.Resolution, nameof(i));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)j, (uint)Grid.Resolution, nameof(j));
        int k = (j * Grid.Resolution) + i;
        return _depth[k] == 0 ? null : (_ground[k] + _depth[k]) * MetersPerUnit;
    }

    private static double Finite(double value) => double.IsNaN(value) ? 0 : value;

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private string DescribeSources()
    {
        if (Sources.Count == 0)
        {
            return "none";
        }

        int natural = Sources.Count(source => source.Type == WaterSourceRecord.NaturalType);
        int facility = Sources.Count(source => source.Type == WaterSourceRecord.FacilityType);
        int cleaner = Sources.Count(source => source.Type == WaterSourceRecord.CleanerType);
        int other = Sources.Count - natural - facility - cleaner;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Sources.Count}: {natural} natural, {facility} of buildings, {cleaner} of water treatment{(other == 0 ? string.Empty : $", {other} of unknown types")}");
    }
}
