using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Water;

/// <summary>
/// Sorts the water simulation's cells within an area into the sea, flat lakes and the rest (docs/architecture.md,
/// D39; docs/research/cs1-save-and-world.md, Water).
/// </summary>
/// <remarks>
/// <para>
/// A cell holds water if the game draws some: more than <see cref="CityWater.DrawnOffsetUnits"/> deep. The sea is
/// the water within <see cref="WaterBodyOptions.SeaToleranceMeters"/> of the sea level that is connected to the map's
/// edge through such water; the simulation keeps the edges near the sea level.
/// </para>
/// <para>
/// The other water forms connected bodies, often a river with lakes along it; each body that reaches into the area is
/// followed across the whole map. In each body, the most common surface levels are tried in turn: the cells within
/// <see cref="WaterBodyOptions.LakeBandMeters"/> of such a level, connected to it, are a lake if most of them lie close
/// to their median level and there are enough of them: <see cref="WaterBodyOptions.MinLakeCells"/> for a body that is
/// all lake, <see cref="WaterBodyOptions.MinAttachedLakeCells"/> for a flat part of a larger body, because rivers run
/// through level pools. A lake found is taken out, and the rest is searched again; what remains is left out.
/// </para>
/// <para>
/// A lake's boxes of water cover its cells and the dry cells around it whose ground lies above its surface (or less
/// than <see cref="WaterBodyOptions.ShoreToleranceMeters"/> below it), never other water or lower dry ground, so they
/// can't flood the land beside a lake. They are clipped to the area.
/// </para>
/// </remarks>
public static class WaterBodyFinder
{
    /// <summary>Sorts the water within an area.</summary>
    /// <param name="water">The city's water.</param>
    /// <param name="ground">
    /// The ground the level will have at a city position (X, Z), in metres; it decides which dry cells around a lake its
    /// water may cover, and which lie below the sea.
    /// </param>
    /// <param name="area">The area, in city X and Z, normally the terrain's coverage.</param>
    /// <param name="options">Thresholds, or <see langword="null"/> for <see cref="WaterBodyOptions.Default"/>.</param>
    /// <returns>The sorted water.</returns>
    public static WaterBodies Find(CityWater water, Func<double, double, double> ground, MapRect area, WaterBodyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(water);
        ArgumentNullException.ThrowIfNull(ground);
        var analysis = new Analysis(water, ground, area, options ?? WaterBodyOptions.Default);
        return analysis.Run();
    }

    // The state of one analysis: the cells' levels, the masks, and the results so far.
    private sealed class Analysis
    {
        private const int NotWet = int.MinValue;

        private readonly CityWater _water;
        private readonly Func<double, double, double> _ground;
        private readonly MapRect _area;
        private readonly WaterBodyOptions _options;
        private readonly GridGeometry _grid;
        private readonly int _resolution;

        // Each cell's surface (ground plus depth) in 1/64 m where the game draws water, else NotWet.
        private readonly int[] _level;
        private readonly bool[] _sea;

        // The cells within the area: those whose square overlaps it. Bodies of water are followed beyond it.
        private readonly int _firstColumn;
        private readonly int _lastColumn;
        private readonly int _firstRow;
        private readonly int _lastRow;

        // Marks set by the searches, and the cells a flood fill has visited; a mark or visit is current while it equals
        // the search's own stamp, so neither array needs clearing.
        private readonly int[] _marks;
        private readonly int[] _visits;
        private readonly bool[] _assigned;
        private int _stamp;

        private readonly List<Lake> _lakes = [];
        private int _skippedBodies;
        private int _skippedCells;
        private int _smallCells;
        private int _skippedLowest = int.MaxValue;
        private int _skippedHighest = int.MinValue;

        public Analysis(CityWater water, Func<double, double, double> ground, MapRect area, WaterBodyOptions options)
        {
            _water = water;
            _ground = ground;
            _area = area;
            _options = options;
            _grid = water.Grid;
            _resolution = _grid.Resolution;
            int count = _grid.PointCount;
            _level = new int[count];
            _sea = new bool[count];
            _marks = new int[count];
            _visits = new int[count];
            _assigned = new bool[count];
            ReadOnlySpan<ushort> groundUnits = water.Ground;
            ReadOnlySpan<ushort> depth = water.Depth;
            for (int k = 0; k < count; k++)
            {
                _level[k] = depth[k] > CityWater.DrawnOffsetUnits ? groundUnits[k] + depth[k] : NotWet;
            }

            double half = _grid.Spacing / 2.0;
            _firstColumn = ClampIndex(Math.Floor((area.MinX - half - _grid.OriginX) / _grid.Spacing));
            _lastColumn = ClampIndex(Math.Ceiling((area.MaxX + half - _grid.OriginX) / _grid.Spacing));
            _firstRow = ClampIndex(Math.Floor((area.MinZ - half - _grid.OriginZ) / _grid.Spacing));
            _lastRow = ClampIndex(Math.Ceiling((area.MaxZ + half - _grid.OriginZ) / _grid.Spacing));
        }

        public WaterBodies Run()
        {
            bool hasSea = FindSea();
            double seaSurface = (_water.SeaLevel / CityWater.MetersPerUnit - CityWater.DrawnOffsetUnits) * CityWater.MetersPerUnit;
            int seaCells = 0;
            int dryBelowSea = 0;
            for (int j = _firstRow; j <= _lastRow; j++)
            {
                for (int i = _firstColumn; i <= _lastColumn; i++)
                {
                    int k = Index(i, j);
                    if (_sea[k])
                    {
                        seaCells++;
                    }
                    else if (hasSea && _level[k] == NotWet && GroundAt(i, j) < seaSurface)
                    {
                        dryBelowSea++;
                    }
                }
            }

            for (int j = _firstRow; j <= _lastRow; j++)
            {
                for (int i = _firstColumn; i <= _lastColumn; i++)
                {
                    int k = Index(i, j);
                    if (_level[k] != NotWet && !_sea[k] && !_assigned[k])
                    {
                        ExtractLakes(Component(k, cell => _level[cell] != NotWet && !_sea[cell] && !_assigned[cell], NextStamp()));
                    }
                }
            }

            return new WaterBodies
            {
                SeaLevel = _water.SeaLevel,
                SeaSurface = hasSea ? seaSurface : null,
                SeaCells = seaCells,
                Lakes = _lakes,
                SkippedBodies = _skippedBodies,
                SkippedCells = _skippedCells,
                SkippedLowest = _skippedBodies == 0 ? 0 : (_skippedLowest - CityWater.DrawnOffsetUnits) * CityWater.MetersPerUnit,
                SkippedHighest = _skippedBodies == 0 ? 0 : (_skippedHighest - CityWater.DrawnOffsetUnits) * CityWater.MetersPerUnit,
                SmallWaterCells = _smallCells,
                DryCellsBelowSea = dryBelowSea,
                CellAreaSquareMeters = (double)_grid.Spacing * _grid.Spacing,
            };
        }

        // Floods the sea from the map's edge; returns whether there is any.
        private bool FindSea()
        {
            double seaUnits = _water.SeaLevel / CityWater.MetersPerUnit;
            double tolerance = _options.SeaToleranceMeters / CityWater.MetersPerUnit;
            bool IsSeaLevel(int k) => _level[k] != NotWet && Math.Abs(_level[k] - seaUnits) <= tolerance;

            var pending = new Stack<int>();
            int last = _resolution - 1;
            for (int t = 0; t <= last; t++)
            {
                foreach (int k in (ReadOnlySpan<int>)[Index(t, 0), Index(t, last), Index(0, t), Index(last, t)])
                {
                    if (!_sea[k] && IsSeaLevel(k))
                    {
                        _sea[k] = true;
                        pending.Push(k);
                    }
                }
            }

            bool any = pending.Count > 0;
            while (pending.Count > 0)
            {
                int k = pending.Pop();
                int i = k % _resolution;
                int j = k / _resolution;
                foreach ((int ni, int nj) in Neighbours(i, j, 0, last, 0, last))
                {
                    int n = Index(ni, nj);
                    if (!_sea[n] && IsSeaLevel(n))
                    {
                        _sea[n] = true;
                        pending.Push(n);
                    }
                }
            }

            return any;
        }

        // Takes the lakes out of a body of water; what isn't a lake is left out.
        private void ExtractLakes(List<int> body)
        {
            var pending = new Queue<List<int>>();
            pending.Enqueue(body);
            while (pending.Count > 0)
            {
                List<int> cells = pending.Dequeue();
                foreach (int k in cells)
                {
                    _assigned[k] = true;
                }

                List<int>? lake = cells.Count >= _options.MinLakeCells ? FindLake(cells) : null;
                if (lake is null)
                {
                    Skip(cells);
                    continue;
                }

                AddLake(lake);
                int stamp = NextStamp();
                foreach (int k in lake)
                {
                    _marks[k] = stamp;
                }

                // The rest of the body falls apart into pieces; each is searched on its own.
                int rest = NextStamp();
                foreach (int k in cells)
                {
                    if (_marks[k] != stamp)
                    {
                        _marks[k] = rest;
                    }
                }

                int visit = NextStamp();
                foreach (int k in cells)
                {
                    if (_marks[k] == rest && _visits[k] != visit)
                    {
                        pending.Enqueue(Component(k, cell => _marks[cell] == rest, visit));
                    }
                }
            }
        }

        // The largest lake around the body's most common levels, or null if none of them makes a lake.
        private List<int>? FindLake(List<int> cells)
        {
            double binUnits = Math.Max(1, _options.LevelBinMeters / CityWater.MetersPerUnit);
            var counts = new Dictionary<int, int>();
            foreach (int k in cells)
            {
                int bin = (int)Math.Floor(_level[k] / binUnits);
                counts[bin] = counts.TryGetValue(bin, out int count) ? count + 1 : 1;
            }

            var bins = new List<(int Bin, int Count)>(counts.Count);
            foreach (KeyValuePair<int, int> entry in counts)
            {
                bins.Add((entry.Key, entry.Value));
            }

            bins.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a.Bin.CompareTo(b.Bin));
            int minSeed = Math.Max(1, _options.MinLakeCells / 4);
            double band = _options.LakeBandMeters / CityWater.MetersPerUnit;
            foreach ((int bin, int count) in bins)
            {
                if (count < minSeed)
                {
                    break;
                }

                double centre = (bin + 0.5) * binUnits;
                int inBand = NextStamp();
                foreach (int k in cells)
                {
                    if (Math.Abs(_level[k] - centre) <= band)
                    {
                        _marks[k] = inBand;
                    }
                }

                // The largest connected group of band cells around the bin's own cells.
                List<int>? best = null;
                int visit = NextStamp();
                foreach (int k in cells)
                {
                    if (_marks[k] == inBand && _visits[k] != visit && (int)Math.Floor(_level[k] / binUnits) == bin)
                    {
                        List<int> group = Component(k, cell => _marks[cell] == inBand, visit);
                        if (best is null || group.Count > best.Count)
                        {
                            best = group;
                        }
                    }
                }

                // A flat part of a larger body, such as a pool along a river, must be large to count as a lake.
                int minCells = cells.Count - (best?.Count ?? 0) >= _options.MinLakeCells ? _options.MinAttachedLakeCells : _options.MinLakeCells;
                if (best is not null && best.Count >= minCells && IsFlat(best))
                {
                    return best;
                }
            }

            return null;
        }

        private bool IsFlat(List<int> cells)
        {
            int[] levels = new int[cells.Count];
            for (int n = 0; n < levels.Length; n++)
            {
                levels[n] = _level[cells[n]];
            }

            Array.Sort(levels);
            int median = levels[levels.Length / 2];
            double core = _options.LakeCoreMeters / CityWater.MetersPerUnit;
            int close = 0;
            foreach (int level in levels)
            {
                if (Math.Abs(level - median) <= core)
                {
                    close++;
                }
            }

            return close >= _options.LakeCoreFraction * levels.Length;
        }

        private void AddLake(List<int> cells)
        {
            int[] levels = new int[cells.Count];
            int minI = int.MaxValue;
            int maxI = int.MinValue;
            int minJ = int.MaxValue;
            int maxJ = int.MinValue;
            int lakeStamp = NextStamp();
            for (int n = 0; n < cells.Count; n++)
            {
                int k = cells[n];
                levels[n] = _level[k];
                _marks[k] = lakeStamp;
                minI = Math.Min(minI, k % _resolution);
                maxI = Math.Max(maxI, k % _resolution);
                minJ = Math.Min(minJ, k / _resolution);
                maxJ = Math.Max(maxJ, k / _resolution);
            }

            if (maxI < _firstColumn || minI > _lastColumn || maxJ < _firstRow || minJ > _lastRow)
            {
                return;
            }

            Array.Sort(levels);
            double surface = (levels[levels.Length / 2] - CityWater.DrawnOffsetUnits) * CityWater.MetersPerUnit;

            // Rectangles over the lake's cells and the dry cells around it whose ground stands above its surface,
            // which hide the water; never over other water or over dry ground lower than the lake.
            minI = Math.Max(minI - 1, 0);
            maxI = Math.Min(maxI + 1, _resolution - 1);
            minJ = Math.Max(minJ - 1, 0);
            maxJ = Math.Min(maxJ + 1, _resolution - 1);
            int used = NextStamp();
            double lowestShore = surface - _options.ShoreToleranceMeters;
            bool Allowed(int i, int j)
            {
                int k = Index(i, j);
                return _marks[k] != used && (_marks[k] == lakeStamp || (_level[k] == NotWet && GroundAt(i, j) >= lowestShore));
            }

            var rectangles = new List<MapRect>();
            double bottom = double.MaxValue;
            for (int j = minJ; j <= maxJ; j++)
            {
                for (int i = minI; i <= maxI; i++)
                {
                    if (_marks[Index(i, j)] != lakeStamp)
                    {
                        continue;
                    }

                    // The widest run of allowed cells through this one, then as many rows up and down as fit the whole run.
                    int startI = i;
                    while (startI > minI && Allowed(startI - 1, j))
                    {
                        startI--;
                    }

                    int endI = i;
                    while (endI < maxI && Allowed(endI + 1, j))
                    {
                        endI++;
                    }

                    int endJ = j;
                    while (endJ < maxJ && RowAllowed(startI, endI, endJ + 1))
                    {
                        endJ++;
                    }

                    int startJ = j;
                    while (startJ > minJ && RowAllowed(startI, endI, startJ - 1))
                    {
                        startJ--;
                    }

                    for (int rj = startJ; rj <= endJ; rj++)
                    {
                        for (int ri = startI; ri <= endI; ri++)
                        {
                            int k = Index(ri, rj);
                            _marks[k] = used;
                            bottom = Math.Min(bottom, Math.Min(GroundAt(ri, rj), _water.Ground[k] * CityWater.MetersPerUnit));
                        }
                    }

                    if (CellRectangle(startI, endI, startJ, endJ).Intersect(_area) is { IsEmpty: false } clipped)
                    {
                        rectangles.Add(clipped);
                    }
                }
            }

            bool RowAllowed(int fromI, int toI, int j)
            {
                for (int i = fromI; i <= toI; i++)
                {
                    if (!Allowed(i, j))
                    {
                        return false;
                    }
                }

                return true;
            }

            int inArea = CountInArea(cells);
            if (rectangles.Count > 0 && inArea > 0)
            {
                _lakes.Add(new Lake(
                    surface,
                    Math.Min(bottom, surface) - _options.LakeBottomMarginMeters,
                    inArea,
                    inArea * (double)_grid.Spacing * _grid.Spacing,
                    rectangles));
            }
        }

        // Counts the water left out, as far as it lies within the area.
        private void Skip(List<int> cells)
        {
            int inArea = CountInArea(cells);
            if (inArea == 0)
            {
                return;
            }

            if (cells.Count < _options.MinLakeCells)
            {
                _smallCells += inArea;
                return;
            }

            _skippedBodies++;
            _skippedCells += inArea;
            foreach (int k in cells)
            {
                if (IsInArea(k))
                {
                    _skippedLowest = Math.Min(_skippedLowest, _level[k]);
                    _skippedHighest = Math.Max(_skippedHighest, _level[k]);
                }
            }
        }

        private int CountInArea(List<int> cells)
        {
            int count = 0;
            foreach (int k in cells)
            {
                if (IsInArea(k))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsInArea(int k)
        {
            int i = k % _resolution;
            int j = k / _resolution;
            return i >= _firstColumn && i <= _lastColumn && j >= _firstRow && j <= _lastRow;
        }

        // The cells connected to a start cell (4-neighbours) through cells that pass a test, anywhere on the map,
        // sorted. Cells already visited under the same visit stamp are left out, so one stamp can split a set into pieces.
        private List<int> Component(int start, Func<int, bool> member, int visit)
        {
            var cells = new List<int>();
            if (_visits[start] == visit || !member(start))
            {
                return cells;
            }

            var pending = new Stack<int>();
            _visits[start] = visit;
            pending.Push(start);
            while (pending.Count > 0)
            {
                int k = pending.Pop();
                cells.Add(k);
                foreach ((int ni, int nj) in Neighbours(k % _resolution, k / _resolution, 0, _resolution - 1, 0, _resolution - 1))
                {
                    int n = Index(ni, nj);
                    if (_visits[n] != visit && member(n))
                    {
                        _visits[n] = visit;
                        pending.Push(n);
                    }
                }
            }

            cells.Sort();
            return cells;
        }

        private int NextStamp() => ++_stamp;

        private int Index(int i, int j) => (j * _resolution) + i;

        private int ClampIndex(double value) => (int)Math.Clamp(value, 0, _resolution - 1);

        private double GroundAt(int i, int j) =>
            _ground(_grid.OriginX + ((double)i * _grid.Spacing), _grid.OriginZ + ((double)j * _grid.Spacing));

        private MapRect CellRectangle(int firstI, int lastI, int firstJ, int lastJ)
        {
            double half = _grid.Spacing / 2.0;
            return new MapRect(
                (float)(_grid.OriginX + (firstI * (double)_grid.Spacing) - half),
                (float)(_grid.OriginZ + (firstJ * (double)_grid.Spacing) - half),
                (float)(_grid.OriginX + (lastI * (double)_grid.Spacing) + half),
                (float)(_grid.OriginZ + (lastJ * (double)_grid.Spacing) + half));
        }

        private static IEnumerable<(int I, int J)> Neighbours(int i, int j, int minI, int maxI, int minJ, int maxJ)
        {
            if (i > minI)
            {
                yield return (i - 1, j);
            }

            if (i < maxI)
            {
                yield return (i + 1, j);
            }

            if (j > minJ)
            {
                yield return (i, j - 1);
            }

            if (j < maxJ)
            {
                yield return (i, j + 1);
            }
        }
    }
}
