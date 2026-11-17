using System;

namespace CsToBeamNG.Converter.Coordinates;

/// <summary>
/// Sine and cosine from additions, multiplications and rounding only, so they give the same bits on every
/// platform. <see cref="Math.Sin"/> and <see cref="Math.Cos"/> come from each platform's C library, whose last
/// bits can differ, and generated output must not (agents/conventions.md, Data, coordinates and determinism).
/// </summary>
/// <remarks>
/// The angle is reduced to |r| ≤ π/4 around the nearest multiple of π/2 (Cody and Waite's method), and the
/// Taylor series of sine and cosine are summed up to the 17th and 18th power, whose remainder is below 10⁻¹⁹ on
/// that interval. The results are within a few units in the last place of a double, plenty for the floats that
/// BeamNG stores. .NET doesn't fuse multiplications and additions on its own, so the arithmetic is exactly
/// IEEE 754 everywhere.
/// </remarks>
internal static class DeterministicTrig
{
    /// <summary>The largest angle magnitude accepted. Up to it, the multiple of π/2 fits in 20 bits, which keeps the reduction exact.</summary>
    public const double MaxAngle = 1e6;

    // π/2 = Part1 + Part2 + Part3. Part1 and Part2 have at most 33 significant bits, so k × Part1 and k × Part2
    // are exact for |k| < 2^20; Part3 is the rest, rounded. Computed from π to 110 digits.
    internal const double PiOver2Part1 = 1.5707963267341256;
    internal const double PiOver2Part2 = 6.077100506303966e-11;
    internal const double PiOver2Part3 = 2.0222662487959506e-21;

    private const double TwoOverPi = 0.6366197723675814;

    // Taylor coefficients: ±1/n! for odd n (sine) and even n (cosine). The factorials up to 18! are exact doubles.
    private const double Sin3 = -1.0 / 6;
    private const double Sin5 = 1.0 / 120;
    private const double Sin7 = -1.0 / 5040;
    private const double Sin9 = 1.0 / 362880;
    private const double Sin11 = -1.0 / 39916800;
    private const double Sin13 = 1.0 / 6227020800;
    private const double Sin15 = -1.0 / 1307674368000;
    private const double Sin17 = 1.0 / 355687428096000;
    private const double Cos2 = -1.0 / 2;
    private const double Cos4 = 1.0 / 24;
    private const double Cos6 = -1.0 / 720;
    private const double Cos8 = 1.0 / 40320;
    private const double Cos10 = -1.0 / 3628800;
    private const double Cos12 = 1.0 / 479001600;
    private const double Cos14 = -1.0 / 87178291200;
    private const double Cos16 = 1.0 / 20922789888000;
    private const double Cos18 = -1.0 / 6402373705728000;

    /// <summary>Returns the sine and cosine of an angle.</summary>
    /// <param name="radians">The angle in radians, at most <see cref="MaxAngle"/> in magnitude.</param>
    /// <exception cref="ArgumentOutOfRangeException">The angle isn't finite or is too large.</exception>
    public static (double Sin, double Cos) SinCos(double radians)
    {
        if (!(Math.Abs(radians) <= MaxAngle))
        {
            throw new ArgumentOutOfRangeException(nameof(radians), radians, "The angle must be finite and at most 1e6 radians in magnitude.");
        }

        double quadrant = Math.Round(radians * TwoOverPi);
        double r = radians - (quadrant * PiOver2Part1) - (quadrant * PiOver2Part2) - (quadrant * PiOver2Part3);
        double z = r * r;

        // Horner's scheme in r², from the highest power down.
        double sinTail = Sin17;
        sinTail = Sin15 + (z * sinTail);
        sinTail = Sin13 + (z * sinTail);
        sinTail = Sin11 + (z * sinTail);
        sinTail = Sin9 + (z * sinTail);
        sinTail = Sin7 + (z * sinTail);
        sinTail = Sin5 + (z * sinTail);
        sinTail = Sin3 + (z * sinTail);
        double sin = r + (r * z * sinTail);

        double cosTail = Cos18;
        cosTail = Cos16 + (z * cosTail);
        cosTail = Cos14 + (z * cosTail);
        cosTail = Cos12 + (z * cosTail);
        cosTail = Cos10 + (z * cosTail);
        cosTail = Cos8 + (z * cosTail);
        cosTail = Cos6 + (z * cosTail);
        cosTail = Cos4 + (z * cosTail);
        cosTail = Cos2 + (z * cosTail);
        double cos = 1 + (z * cosTail);

        // The angle is r plus quadrant quarter turns; |quadrant| < 2^20, so the conversion is exact.
        return ((long)quadrant & 3) switch
        {
            0 => (sin, cos),
            1 => (cos, -sin),
            2 => (-sin, -cos),
            _ => (-cos, sin),
        };
    }
}
