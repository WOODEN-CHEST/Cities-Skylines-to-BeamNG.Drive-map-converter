namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// Piecewise cubic interpolation of evenly spaced values that never overshoots: between two values it stays within
/// their range, and where the data turn (a peak or a valley at a point) the curve is flat there. It passes through
/// every value and reproduces straight lines exactly.
/// </summary>
/// <remarks>
/// <para>
/// A Hermite cubic whose slope at each point is the harmonic mean of the two neighbouring differences, or 0 where they
/// differ in sign (Fritsch and Butland's choice of slopes, the one PCHIP makes on even spacing). Unlike Catmull-Rom,
/// it doesn't dip below a shore or bulge above a ridge between two grid points.
/// </para>
/// <para>
/// The game interpolates its coarse heights with a cubic of the same kind (slopes limited by the neighbouring
/// differences; docs/research/cs1-save-and-world.md, Samplers), but draws them bilinearly. The converter smooths the
/// 16 m heights with this one so that they don't show the 16 m grid as facets at BeamNG's finer resolution
/// (docs/architecture.md, D37). Only arithmetic is used, so results are the same on every platform.
/// </para>
/// </remarks>
public static class MonotoneCubic
{
    /// <summary>Interpolates between <paramref name="p1"/> and <paramref name="p2"/>.</summary>
    /// <param name="p0">The value one step before <paramref name="p1"/>.</param>
    /// <param name="p1">The value at t = 0.</param>
    /// <param name="p2">The value at t = 1.</param>
    /// <param name="p3">The value one step after <paramref name="p2"/>.</param>
    /// <param name="t">The position between <paramref name="p1"/> and <paramref name="p2"/>, from 0 to 1.</param>
    /// <returns>The interpolated value.</returns>
    public static double Interpolate(double p0, double p1, double p2, double p3, double t)
    {
        double before = p1 - p0;
        double between = p2 - p1;
        double after = p3 - p2;
        double slope1 = Slope(before, between);
        double slope2 = Slope(between, after);
        double t2 = t * t;
        double t3 = t2 * t;

        // The Hermite basis: values at both ends and slopes at both ends.
        double h00 = (2 * t3) - (3 * t2) + 1;
        double h10 = t3 - (2 * t2) + t;
        double h01 = (3 * t2) - (2 * t3);
        double h11 = t3 - t2;
        return (h00 * p1) + (h10 * slope1) + (h01 * p2) + (h11 * slope2);
    }

    // The harmonic mean of two neighbouring differences, or 0 where they differ in sign or one is 0.
    private static double Slope(double left, double right) =>
        left * right > 0 ? 2 * left * right / (left + right) : 0;
}
