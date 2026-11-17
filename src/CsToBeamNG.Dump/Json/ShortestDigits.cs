using System;

namespace CsToBeamNG.Dump.Json;

/// <summary>
/// Finds the shortest decimal digits that read back as a given binary floating-point value and, of those,
/// the ones closest to its exact value.
/// </summary>
/// <remarks>
/// <para>
/// This is the free-format algorithm of Burger and Dybvig, "Printing Floating-Point Numbers Quickly and
/// Accurately" (PLDI 1996), with exact big-integer arithmetic. A decimal reads back as the value if it lies
/// between the midpoints to the neighbouring values. A midpoint itself counts when the value's mantissa is
/// even, because parsers round ties to even. When two final digits are equally close, the even one wins.
/// </para>
/// <para>
/// It doesn't use the runtime's number formatting or parsing. On older runtimes both can be off by one
/// unit in the last place, in ways that agree with each other, so checking a result by parsing it back
/// doesn't help: Mono 6.8, for example, writes some doubles with 15 digits that a correct parser reads as
/// the neighbouring double. Integer arithmetic gives the same digits in the game and on .NET 10, where the
/// tests compare them with <c>ToString("R")</c>.
/// </para>
/// </remarks>
internal static class ShortestDigits
{
    /// <summary>The most digits a double needs. A float needs at most 9.</summary>
    public const int MaxDigits = 17;

    // To estimate the decimal exponent from the binary one; the estimate is then corrected exactly.
    private const double Log10Of2 = 0.30102999566398119521;

    /// <summary>Generates the digits of the positive value <c>mantissa * 2^exponent</c>.</summary>
    /// <param name="mantissa">The integer mantissa, greater than 0.</param>
    /// <param name="exponent">The binary exponent.</param>
    /// <param name="lowerGapIsSmaller">
    /// Whether the next smaller value is closer than the next larger one. That is the case for powers of two,
    /// except the smallest normal number.
    /// </param>
    /// <param name="digits">Receives the digits; the first one isn't '0'. At least <see cref="MaxDigits"/> long.</param>
    /// <param name="decimalExponent">The exponent k for which the value is 0.d1d2d3... * 10^k.</param>
    /// <returns>The number of digits.</returns>
    public static int Generate(ulong mantissa, int exponent, bool lowerGapIsSmaller, char[] digits, out int decimalExponent)
    {
        // The value is r/s. The midpoints to its neighbours lie mMinus/s below it and mPlus/s above it.
        var r = new Natural();
        var s = new Natural();
        var mPlus = new Natural();
        var mMinus = new Natural();
        var scratch = new Natural();
        int gapShift = lowerGapIsSmaller ? 1 : 0;
        r.SetValue(mantissa);
        mPlus.SetValue(1);
        mMinus.SetValue(1);
        if (exponent >= 0)
        {
            r.ShiftLeft(exponent + 1 + gapShift);
            s.SetValue(2u << gapShift);
            mPlus.ShiftLeft(exponent + gapShift);
            mMinus.ShiftLeft(exponent);
        }
        else
        {
            r.ShiftLeft(1 + gapShift);
            s.SetValue(1);
            s.ShiftLeft(1 + gapShift - exponent);
            mPlus.ShiftLeft(gapShift);
        }

        bool even = (mantissa & 1) == 0;
        int k = EstimateDecimalExponent(mantissa, exponent);
        if (k >= 0)
        {
            s.MultiplyByPowerOfTen(k);
        }
        else
        {
            r.MultiplyByPowerOfTen(-k);
            mPlus.MultiplyByPowerOfTen(-k);
            mMinus.MultiplyByPowerOfTen(-k);
        }

        // Correct k: the upper midpoint must lie below 10^k (or at it, if it counts), and not below 10^(k-1).
        while (true)
        {
            scratch.SetSum(r, mPlus);
            int comparison = Natural.Compare(scratch, s);
            if (comparison < 0 || (comparison == 0 && !even))
            {
                break;
            }

            s.MultiplyBy(10);
            k++;
        }

        while (true)
        {
            scratch.SetSum(r, mPlus);
            scratch.MultiplyBy(10);
            int comparison = Natural.Compare(scratch, s);
            if (comparison > 0 || (comparison == 0 && even))
            {
                break;
            }

            r.MultiplyBy(10);
            mPlus.MultiplyBy(10);
            mMinus.MultiplyBy(10);
            k--;
        }

        decimalExponent = k;
        int count = 0;
        while (true)
        {
            r.MultiplyBy(10);
            mPlus.MultiplyBy(10);
            mMinus.MultiplyBy(10);
            int digit = 0;
            while (Natural.Compare(r, s) >= 0)
            {
                r.Subtract(s);
                digit++;
            }

            // Whether the digits can end here, with this digit (low) or the next higher one (high).
            int lowComparison = Natural.Compare(r, mMinus);
            bool low = lowComparison < 0 || (lowComparison == 0 && even);
            scratch.SetSum(r, mPlus);
            int highComparison = Natural.Compare(scratch, s);
            bool high = highComparison > 0 || (highComparison == 0 && even);
            if (low && high)
            {
                // Both read back correctly: take the closer one, and on a tie the even digit.
                scratch.CopyFrom(r);
                scratch.ShiftLeft(1);
                int comparison = Natural.Compare(scratch, s);
                if (comparison > 0 || (comparison == 0 && (digit & 1) == 1))
                {
                    digit++;
                }
            }
            else if (high)
            {
                digit++;
            }

            digits[count++] = (char)('0' + digit);
            if (low || high)
            {
                return count;
            }
        }
    }

    private static int EstimateDecimalExponent(ulong mantissa, int exponent)
    {
        int bitLength = 0;
        for (ulong rest = mantissa; rest != 0; rest >>= 1)
        {
            bitLength++;
        }

        // The value lies in [2^(exponent + bitLength - 1), 2^(exponent + bitLength)).
        return (int)Math.Ceiling(((exponent + bitLength - 1) * Log10Of2) - 1e-10);
    }

    /// <summary>A natural number of any size, as little-endian 32-bit words without leading zero words.</summary>
    private sealed class Natural
    {
        private const uint WordBits = 32;
        private const uint NineDigits = 1_000_000_000;

        private static readonly uint[] _smallPowersOfTen = [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];

        private uint[] _words = new uint[8];
        private int _length;

        public static int Compare(Natural a, Natural b)
        {
            if (a._length != b._length)
            {
                return a._length < b._length ? -1 : 1;
            }

            for (int i = a._length - 1; i >= 0; i--)
            {
                if (a._words[i] != b._words[i])
                {
                    return a._words[i] < b._words[i] ? -1 : 1;
                }
            }

            return 0;
        }

        public void SetValue(ulong value)
        {
            _words[0] = (uint)value;
            _words[1] = (uint)(value >> 32);
            _length = _words[1] != 0 ? 2 : _words[0] != 0 ? 1 : 0;
        }

        public void CopyFrom(Natural other)
        {
            EnsureCapacity(other._length);
            Array.Copy(other._words, _words, other._length);
            _length = other._length;
        }

        /// <summary>Sets this to a + b. Neither may be this instance.</summary>
        public void SetSum(Natural a, Natural b)
        {
            Natural longer = a._length >= b._length ? a : b;
            Natural shorter = ReferenceEquals(longer, a) ? b : a;
            EnsureCapacity(longer._length + 1);
            ulong carry = 0;
            for (int i = 0; i < longer._length; i++)
            {
                ulong sum = longer._words[i] + (i < shorter._length ? shorter._words[i] : 0UL) + carry;
                _words[i] = (uint)sum;
                carry = sum >> 32;
            }

            _length = longer._length;
            if (carry != 0)
            {
                _words[_length++] = (uint)carry;
            }
        }

        /// <summary>Subtracts a number that is not larger than this one.</summary>
        public void Subtract(Natural other)
        {
            ulong borrow = 0;
            for (int i = 0; i < _length; i++)
            {
                ulong subtrahend = (i < other._length ? other._words[i] : 0UL) + borrow;
                ulong word = _words[i];
                borrow = word < subtrahend ? 1UL : 0UL;
                _words[i] = (uint)((word + (borrow << 32)) - subtrahend);
            }

            Trim();
        }

        public void MultiplyBy(uint factor)
        {
            ulong carry = 0;
            for (int i = 0; i < _length; i++)
            {
                ulong product = ((ulong)_words[i] * factor) + carry;
                _words[i] = (uint)product;
                carry = product >> 32;
            }

            if (carry != 0)
            {
                EnsureCapacity(_length + 1);
                _words[_length++] = (uint)carry;
            }
        }

        public void MultiplyByPowerOfTen(int power)
        {
            for (; power >= 9; power -= 9)
            {
                MultiplyBy(NineDigits);
            }

            if (power > 0)
            {
                MultiplyBy(_smallPowersOfTen[power]);
            }
        }

        public void ShiftLeft(int bits)
        {
            if (_length == 0 || bits == 0)
            {
                return;
            }

            int wordShift = bits / (int)WordBits;
            int bitShift = bits % (int)WordBits;
            int newLength = _length + wordShift + (bitShift == 0 ? 0 : 1);
            EnsureCapacity(newLength);
            if (bitShift == 0)
            {
                for (int i = _length - 1; i >= 0; i--)
                {
                    _words[i + wordShift] = _words[i];
                }
            }
            else
            {
                int carryShift = (int)WordBits - bitShift;
                _words[_length + wordShift] = _words[_length - 1] >> carryShift;
                for (int i = _length - 1; i > 0; i--)
                {
                    _words[i + wordShift] = (_words[i] << bitShift) | (_words[i - 1] >> carryShift);
                }

                _words[wordShift] = _words[0] << bitShift;
            }

            Array.Clear(_words, 0, wordShift);
            _length = newLength;
            Trim();
        }

        private void Trim()
        {
            while (_length > 0 && _words[_length - 1] == 0)
            {
                _length--;
            }
        }

        private void EnsureCapacity(int words)
        {
            if (_words.Length < words)
            {
                Array.Resize(ref _words, Math.Max(words, _words.Length * 2));
            }
        }
    }
}
