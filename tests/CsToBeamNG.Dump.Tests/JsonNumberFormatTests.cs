using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsToBeamNG.Dump.Json;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

/// <summary>
/// The JSON writer formats floating-point numbers with its own exact algorithm, so that the exporter writes
/// the same text on the game's old runtime. These tests compare it with .NET's shortest round-trip
/// formatting, which .NET Core 3.0 and later get right except for two powers of two.
/// </summary>
public sealed class JsonNumberFormatTests
{
    [Fact]
    public void FormatsDoublesExactlyLikeDotNet()
    {
        var random = new Random(1117);
        var values = new List<double>();

        // Random bit patterns cover every exponent, subnormals included.
        while (values.Count < 100_000)
        {
            double value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            if (double.IsFinite(value))
            {
                values.Add(value);
            }
        }

        // Powers of two have a smaller gap below them, except the smallest normal number.
        for (int exponent = -1074; exponent <= 1023; exponent++)
        {
            AddWithNeighbours(values, Math.ScaleB(1.0, exponent));
        }

        // Powers of ten, around the switches to exponent notation.
        for (int exponent = -323; exponent <= 308; exponent++)
        {
            AddWithNeighbours(values, double.Parse("1e" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }

        // Values like the game's: positions on a 1/64 m grid, decimal fractions, integers near 2^53.
        for (int i = 0; i < 20_000; i++)
        {
            values.Add(random.Next(-552_960, 552_960) / 64.0);
            values.Add(random.Next(-100_000, 100_000) / 100.0);
            values.Add((double)((1L << 53) - random.Next(0, 1000)));
        }

        values.AddRange([double.MaxValue, double.Epsilon, 2.2250738585072014E-308, 2.2250738585072009E-308, 0.1, 0.2, 0.3, 1.0 / 3]);

        AssertSameAsDotNet(
            values,
            value => value.ToString("R", CultureInfo.InvariantCulture),
            text => BitConverter.DoubleToInt64Bits(double.Parse(text, CultureInfo.InvariantCulture)),
            value => BitConverter.DoubleToInt64Bits(value),
            (writer, value) => writer.WriteNumberValue(value));
    }

    // .NET 10.0.12 writes these two with 16 digits that read back as the next lower double. The shortest
    // text that reads back correctly has 17 digits; Python's repr() gives the same.
    [Theory]
    [InlineData(-25, "2.9802322387695312E-08")]
    [InlineData(-958, "4.1045368012983762E-289")]
    public void WritesPowersOfTwoThatDotNetGetsWrongCorrectly(int exponent, string expected)
    {
        double value = Math.ScaleB(1.0, exponent);
        string written = Write(writer => writer.WriteNumberValue(value));

        Assert.Equal(expected, written);
        Assert.Equal(value, double.Parse(written, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatsFloatsExactlyLikeDotNet()
    {
        var random = new Random(1118);
        var values = new List<float>();
        while (values.Count < 100_000)
        {
            float value = BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
            if (float.IsFinite(value))
            {
                values.Add(value);
            }
        }

        for (int exponent = -149; exponent <= 127; exponent++)
        {
            AddWithNeighbours(values, MathF.ScaleB(1f, exponent));
        }

        for (int exponent = -45; exponent <= 38; exponent++)
        {
            AddWithNeighbours(values, float.Parse("1e" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }

        for (int i = 0; i < 20_000; i++)
        {
            values.Add(random.Next(-552_960, 552_960) / 64f);
            values.Add(random.Next(-100_000, 100_000) / 100f);
            values.Add(random.NextSingle() * 1024f);
        }

        values.AddRange([float.MaxValue, float.Epsilon, 1.17549435E-38f, 1.17549421E-38f, 16777216f, 0.1f, 8640f]);

        AssertSameAsDotNet(
            values,
            value => value.ToString("R", CultureInfo.InvariantCulture),
            text => BitConverter.SingleToInt32Bits(float.Parse(text, CultureInfo.InvariantCulture)),
            value => BitConverter.SingleToInt32Bits(value),
            (writer, value) => writer.WriteNumberValue(value));
    }

    private static void AddWithNeighbours(List<double> values, double value)
    {
        values.AddRange([value, Math.BitDecrement(value), Math.BitIncrement(value), -value]);
    }

    private static void AddWithNeighbours(List<float> values, float value)
    {
        values.AddRange([value, MathF.BitDecrement(value), MathF.BitIncrement(value), -value]);
    }

    // Every written number must read back as the same bits, and must be .NET's text wherever .NET's
    // text reads back correctly itself.
    private static void AssertSameAsDotNet<T>(
        List<T> values,
        Func<T, string> dotNet,
        Func<string, long> parseBits,
        Func<T, long> bits,
        Action<JsonWriter, T> write)
    {
        // One array, one value per line.
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = new JsonWriter(text, leaveOpen: true))
        {
            writer.WriteStartArray();
            foreach (T value in values)
            {
                write(writer, value);
            }

            writer.WriteEndArray();
        }

        string[] lines = text.ToString().Split('\n');
        var problems = new List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            string written = lines[i + 1].Trim().TrimEnd(',');
            string expected = dotNet(values[i]);
            if (parseBits(written) != bits(values[i]))
            {
                problems.Add(written + " doesn't read back as " + expected);
            }
            else if (written != expected && parseBits(expected) == bits(values[i]))
            {
                problems.Add(expected + " written as " + written);
            }
        }

        Assert.True(problems.Count == 0, problems.Count + " of " + values.Count + " are wrong, such as: " + string.Join("; ", problems.Take(10)));
    }

    private static string Write(Action<JsonWriter> write)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = new JsonWriter(text, leaveOpen: true))
        {
            write(writer);
        }

        return text.ToString().TrimEnd('\n');
    }
}
