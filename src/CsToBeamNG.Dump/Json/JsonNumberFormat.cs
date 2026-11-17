using System;
using System.Globalization;
using System.Text;

namespace CsToBeamNG.Dump.Json;

/// <summary>Formats floating-point numbers for JSON (docs/dump-format.md, JSON files).</summary>
/// <remarks>
/// The digits are the shortest that read back as the same value (<see cref="ShortestDigits"/>), computed
/// without the runtime's number formatting, so the text is the same on every runtime. The layout is that
/// of <c>ToString("R")</c> on .NET Core 3.0 and later, which the tests compare against: plain notation,
/// or <c>d.dddE+xx</c> when the decimal point would be more than 17 digits (9 for a float) right of the
/// first digit, or more than 3 zeros left of it.
/// </remarks>
internal static class JsonNumberFormat
{
    // IEEE 754 binary64: the value is mantissa * 2^(biased exponent - 1075), with an implicit leading 1
    // except for subnormals (biased exponent 0, scaled like biased exponent 1).
    private const int DoubleFractionBits = 52;
    private const int DoubleExponentMask = 0x7FF;
    private const int DoubleExponentOffset = 1075;
    private const int DoubleMaxPlainDigits = 17;

    // IEEE 754 binary32, likewise.
    private const int SingleFractionBits = 23;
    private const int SingleExponentMask = 0xFF;
    private const int SingleExponentOffset = 150;
    private const int SingleMaxPlainDigits = 9;

    private const int MinPlainDecimalExponent = -3;

    /// <summary>The shortest text that reads back as the same double, such as <c>0.1</c> or <c>1E+21</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite, which JSON can't represent.</exception>
    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON can't represent NaN or infinity.");
        }

        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        bool negative = (bits >> 63) != 0;
        int biasedExponent = (int)(bits >> DoubleFractionBits) & DoubleExponentMask;
        ulong fraction = bits & ((1UL << DoubleFractionBits) - 1);
        if (biasedExponent == 0 && fraction == 0)
        {
            return negative ? "-0" : "0";
        }

        ulong mantissa = biasedExponent == 0 ? fraction : fraction | (1UL << DoubleFractionBits);
        int exponent = Math.Max(biasedExponent, 1) - DoubleExponentOffset;
        return Layout(negative, mantissa, exponent, fraction == 0 && biasedExponent > 1, DoubleMaxPlainDigits);
    }

    /// <summary>
    /// The shortest text that reads back as the same float, such as <c>0.1</c> for <c>0.1f</c>. Readers parse it
    /// as a 32-bit float.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite, which JSON can't represent.</exception>
    public static string Format(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON can't represent NaN or infinity.");
        }

        uint bits = FloatBits.ToBits(value);
        bool negative = (bits >> 31) != 0;
        int biasedExponent = (int)(bits >> SingleFractionBits) & SingleExponentMask;
        uint fraction = bits & ((1U << SingleFractionBits) - 1);
        if (biasedExponent == 0 && fraction == 0)
        {
            return negative ? "-0" : "0";
        }

        uint mantissa = biasedExponent == 0 ? fraction : fraction | (1U << SingleFractionBits);
        int exponent = Math.Max(biasedExponent, 1) - SingleExponentOffset;
        return Layout(negative, mantissa, exponent, fraction == 0 && biasedExponent > 1, SingleMaxPlainDigits);
    }

    private static string Layout(bool negative, ulong mantissa, int exponent, bool lowerGapIsSmaller, int maxPlainDigits)
    {
        char[] digits = new char[ShortestDigits.MaxDigits];
        int count = ShortestDigits.Generate(mantissa, exponent, lowerGapIsSmaller, digits, out int decimalExponent);
        var text = new StringBuilder(32);
        if (negative)
        {
            text.Append('-');
        }

        if (decimalExponent > maxPlainDigits || decimalExponent < MinPlainDecimalExponent)
        {
            // d.ddd, then the exponent with a sign and at least two digits.
            text.Append(digits[0]);
            if (count > 1)
            {
                text.Append('.').Append(digits, 1, count - 1);
            }

            int power = decimalExponent - 1;
            text.Append(power < 0 ? "E-" : "E+");
            if (power is > -10 and < 10)
            {
                text.Append('0');
            }

            text.Append(Math.Abs(power).ToString(CultureInfo.InvariantCulture));
        }
        else if (decimalExponent <= 0)
        {
            text.Append("0.").Append('0', -decimalExponent).Append(digits, 0, count);
        }
        else if (count <= decimalExponent)
        {
            text.Append(digits, 0, count).Append('0', decimalExponent - count);
        }
        else
        {
            text.Append(digits, 0, decimalExponent).Append('.').Append(digits, decimalExponent, count - decimalExponent);
        }

        return text.ToString();
    }
}
