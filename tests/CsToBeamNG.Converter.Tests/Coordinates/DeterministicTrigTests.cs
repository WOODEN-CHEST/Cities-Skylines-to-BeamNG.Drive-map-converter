using System;
using System.Globalization;
using CsToBeamNG.Converter.Coordinates;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Coordinates;

public sealed class DeterministicTrigTests
{
    // A few units in the last place of a double near 1: far below what a float can show.
    private const double Tolerance = 1e-15;

    [Fact]
    public void MatchesThePlatformLibraryWithinRounding()
    {
        for (int i = -20000; i <= 20000; i++)
        {
            AssertClose(i * 0.001);
        }

        var random = new Random(1117);
        for (int i = 0; i < 20000; i++)
        {
            AssertClose(((random.NextDouble() * 2) - 1) * DeterministicTrig.MaxAngle);
        }
    }

    [Fact]
    public void IsExactAtZero() => Assert.Equal((0.0, 1.0), DeterministicTrig.SinCos(0));

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(2, -1, -1)]
    [InlineData(3, 1, -1)]
    [InlineData(-1, 1, -1)]
    [InlineData(-2, -1, -1)]
    public void HasTheRightSignsInEveryQuadrant(int quarterTurns, int cosSign, int sinSign)
    {
        double angle = (Math.PI / 4) + (quarterTurns * Math.PI / 2);

        (double sin, double cos) = DeterministicTrig.SinCos(angle);

        Assert.Equal(sinSign * Math.Sqrt(0.5), sin, 15);
        Assert.Equal(cosSign * Math.Sqrt(0.5), cos, 15);
    }

    [Fact]
    public void SplitsHalfPiIntoShortParts()
    {
        // The first two parts must leave the low 20 bits of their significands clear, so multiples of them by
        // up to 2^20 are exact.
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(DeterministicTrig.PiOver2Part1) & 0xFFFFF);
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(DeterministicTrig.PiOver2Part2) & 0xFFFFF);
        Assert.Equal(Math.PI / 2, DeterministicTrig.PiOver2Part1 + DeterministicTrig.PiOver2Part2 + DeterministicTrig.PiOver2Part3);
        Assert.True(DeterministicTrig.MaxAngle * 2 / Math.PI < 1 << 20);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e6 + 1)]
    [InlineData(-1e6 - 1)]
    public void RejectsAnglesItCantReduceExactly(double angle) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicTrig.SinCos(angle));

    [Fact]
    public void AcceptsTheLargestAngles()
    {
        AssertClose(DeterministicTrig.MaxAngle);
        AssertClose(-DeterministicTrig.MaxAngle);
    }

    private static void AssertClose(double angle)
    {
        (double sin, double cos) = DeterministicTrig.SinCos(angle);
        Assert.True(Math.Abs(sin - Math.Sin(angle)) <= Tolerance, string.Create(CultureInfo.InvariantCulture, $"sin({angle:R}) = {sin:R}, expected {Math.Sin(angle):R}"));
        Assert.True(Math.Abs(cos - Math.Cos(angle)) <= Tolerance, string.Create(CultureInfo.InvariantCulture, $"cos({angle:R}) = {cos:R}, expected {Math.Cos(angle):R}"));
    }
}
