using System;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Water;

/// <summary>
/// The level of the water near any position: the highest surface the game draws within about two cells of the water
/// grid (16 to 32 m in the game). The terrain's surface rules use it to find shores (docs/architecture.md, D38).
/// </summary>
public sealed class NearbyWater
{
    private readonly GridGeometry _grid;

    // For each grid point, the highest drawn surface among the cells around it (itself and its eight neighbours), or NaN.
    private readonly float[] _surface;

    /// <summary>Prepares the lookup for a city's water.</summary>
    /// <param name="water">The water.</param>
    public NearbyWater(CityWater water)
    {
        ArgumentNullException.ThrowIfNull(water);
        _grid = water.Grid;
        int n = _grid.Resolution;
        float[] drawn = new float[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                drawn[(j * n) + i] = water.DrawnSurfaceAt(i, j) is double surface ? (float)surface : float.NaN;
            }
        }

        _surface = new float[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float highest = float.NaN;
                for (int nj = Math.Max(j - 1, 0); nj <= Math.Min(j + 1, n - 1); nj++)
                {
                    for (int ni = Math.Max(i - 1, 0); ni <= Math.Min(i + 1, n - 1); ni++)
                    {
                        float value = drawn[(nj * n) + ni];
                        if (!float.IsNaN(value) && !(value <= highest))
                        {
                            highest = value;
                        }
                    }
                }

                _surface[(j * n) + i] = highest;
            }
        }
    }

    /// <summary>
    /// Returns the highest drawn water surface near a city position, in metres, or NaN if there is no water nearby.
    /// </summary>
    /// <param name="x">The city X, in metres.</param>
    /// <param name="z">The city Z, in metres.</param>
    public double SurfaceNear(double x, double z)
    {
        int last = _grid.Resolution - 1;
        double gridX = Math.Clamp((x - _grid.OriginX) / _grid.Spacing, 0, last);
        double gridZ = Math.Clamp((z - _grid.OriginZ) / _grid.Spacing, 0, last);
        int i = Math.Min((int)gridX, last - 1);
        int j = Math.Min((int)gridZ, last - 1);
        int n = _grid.Resolution;
        double highest = double.NaN;
        foreach (float value in (ReadOnlySpan<float>)[_surface[(j * n) + i], _surface[(j * n) + i + 1], _surface[((j + 1) * n) + i], _surface[((j + 1) * n) + i + 1]])
        {
            if (!float.IsNaN(value) && !(value <= highest))
            {
                highest = value;
            }
        }

        return highest;
    }
}
