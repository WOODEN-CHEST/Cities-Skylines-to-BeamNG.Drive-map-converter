using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CsToBeamNG.Dump.Json;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class JsonWriterTests
{
    [Fact]
    public void IndentsNestedValuesWithTwoSpacesAndEndsWithANewline()
    {
        string json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("name", "Seaside");
            writer.WriteStartArray("items");
            writer.WriteNumberValue(1);
            writer.WriteStartObject();
            writer.WriteBoolean("on", true);
            writer.WriteNull("nothing");
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteStartObject("emptyObject");
            writer.WriteEndObject();
            writer.WriteStartArray("emptyArray");
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        const string Expected = """
            {
              "name": "Seaside",
              "items": [
                1,
                {
                  "on": true,
                  "nothing": null
                }
              ],
              "emptyObject": {},
              "emptyArray": []
            }

            """;
        Assert.Equal(Expected, json);
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("C:\\maps", "\"C:\\\\maps\"")]
    [InlineData("a\nb\rc\td\be\ff", "\"a\\nb\\rc\\td\\be\\ff\"")]
    [InlineData("a/b", "\"a/b\"")]
    public void EscapesWhatJsonRequires(string value, string expected)
    {
        Assert.Equal(expected + "\n", Write(writer => writer.WriteStringValue(value)));
    }

    [Fact]
    public void EscapesOtherControlCharactersAndJavaScriptLineSeparators()
    {
        string value = new([(char)0x00, (char)0x1F, (char)0x2028, (char)0x2029, (char)0x7F]);

        string expected = "\"" + Escaped("0000") + Escaped("001F") + Escaped("2028") + Escaped("2029") + (char)0x7F + "\"\n";
        Assert.Equal(expected, Write(writer => writer.WriteStringValue(value)));
    }

    [Fact]
    public void KeepsSurrogatePairsAndReplacesUnpairedHalves()
    {
        string emoji = char.ConvertFromUtf32(0x1F600);
        string value = "a" + emoji + "b" + (char)0xD800 + "c" + (char)0xDC00;

        string expected = "\"a" + emoji + "b" + (char)0xFFFD + "c" + (char)0xFFFD + "\"\n";
        Assert.Equal(expected, Write(writer => writer.WriteStringValue(value)));
    }

    [Fact]
    public void WritesFilesAsUtf8WithoutAByteOrderMark()
    {
        using var folder = new TempFolder();
        string path = folder.PathOf("text.json");

        using (JsonWriter writer = JsonWriter.CreateFile(path))
        {
            writer.WriteStringValue("Rīga");
        }

        Assert.Equal([(byte)'"', (byte)'R', 0xC4, 0xAB, (byte)'g', (byte)'a', (byte)'"', (byte)'\n'], File.ReadAllBytes(path));
    }

    [Fact]
    public void CreateFileRefusesToOverwriteAFile()
    {
        using var folder = new TempFolder();
        string path = folder.PathOf("existing.json");
        File.WriteAllText(path, "{}");

        Assert.Throws<IOException>(() => JsonWriter.CreateFile(path));
    }

    [Fact]
    public void WritesIntegersExactly()
    {
        string json = Write(writer =>
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(int.MinValue);
            writer.WriteNumberValue(uint.MaxValue);
            writer.WriteNumberValue(long.MinValue);
            writer.WriteNumberValue(ulong.MaxValue);
            writer.WriteEndArray();
        });

        Assert.Equal("[\n  -2147483648,\n  4294967295,\n  -9223372036854775808,\n  18446744073709551615\n]\n", json);
    }

    [Theory]
    [InlineData(0.1, "0.1")]
    [InlineData(1.0 / 3, "0.3333333333333333")]
    [InlineData(100.0, "100")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(1e21, "1E+21")]
    [InlineData(1e-7, "1E-07")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(double.Epsilon, "5E-324")]
    [InlineData(0.0, "0")]
    [InlineData(-0.0, "-0")]
    public void WritesDoublesInTheShortestFormThatReadsBackTheSame(double value, string expected)
    {
        Assert.Equal(expected + "\n", Write(writer => writer.WriteNumberValue(value)));
    }

    [Theory]
    [InlineData(0.1f, "0.1")]
    [InlineData(16f, "16")]
    [InlineData(123.456f, "123.456")]
    [InlineData(-8640f, "-8640")]
    [InlineData(float.MaxValue, "3.4028235E+38")]
    [InlineData(float.Epsilon, "1E-45")]
    [InlineData(-0f, "-0")]
    public void WritesFloatsInTheShortestFormThatReadsBackAsTheSameFloat(float value, string expected)
    {
        Assert.Equal(expected + "\n", Write(writer => writer.WriteNumberValue(value)));
    }

    [Fact]
    public void NumbersReadBackBitForBitWithSystemTextJson()
    {
        var random = new Random(20261117);
        var doubles = new List<double>();
        while (doubles.Count < 10_000)
        {
            double value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            if (double.IsFinite(value))
            {
                doubles.Add(value);
            }
        }

        var floats = new List<float>();
        while (floats.Count < 10_000)
        {
            float value = BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
            if (float.IsFinite(value))
            {
                floats.Add(value);
            }
        }

        string json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("doubles");
            doubles.ForEach(writer.WriteNumberValue);
            writer.WriteEndArray();
            writer.WriteStartArray("floats");
            floats.ForEach(writer.WriteNumberValue);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        using JsonDocument document = JsonDocument.Parse(json);
        int index = 0;
        foreach (JsonElement element in document.RootElement.GetProperty("doubles").EnumerateArray())
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[index++]), BitConverter.DoubleToInt64Bits(element.GetDouble()));
        }

        index = 0;
        foreach (JsonElement element in document.RootElement.GetProperty("floats").EnumerateArray())
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(floats[index++]), BitConverter.SingleToInt32Bits(element.GetSingle()));
        }
    }

    [Theory]
    [InlineData("lv-LV")]
    [InlineData("de-DE")]
    public void FormatsNumbersTheSameUnderDecimalCommaCultures(string culture)
    {
        const string Expected = "[\n  -1234.5,\n  0.25,\n  1E-07,\n  -1000000\n]\n";
        Assert.Equal(Expected, WriteSampleNumbers());

        using (new CultureScope(culture))
        {
            Assert.Equal(Expected, WriteSampleNumbers());
        }
    }

    [Fact]
    public void RejectsNaNAndInfinityWithoutBreakingTheDocument()
    {
        string json = Write(writer =>
        {
            writer.WriteStartObject();
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteNumber("nan", double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteNumber("infinity", float.PositiveInfinity));
            writer.WriteStartArray("values");
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteNumberValue(double.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteNumberValue(float.NaN));
            writer.WriteNumberValue(1);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        Assert.Equal("{\n  \"values\": [\n    1\n  ]\n}\n", json);
    }

    [Fact]
    public void RejectsInvalidStructureWithoutWritingAnything()
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new JsonWriter(text, leaveOpen: true);

        Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName("atRoot"));
        Assert.Throws<InvalidOperationException>(() => writer.WriteEndObject());
        writer.WriteStartObject();
        Assert.Throws<InvalidOperationException>(() => writer.WriteNumberValue(1));
        Assert.Throws<InvalidOperationException>(() => writer.WriteEndArray());
        writer.WritePropertyName("list");
        Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName("again"));
        Assert.Throws<InvalidOperationException>(() => writer.WriteEndObject());
        writer.WriteStartArray();
        Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName("inArray"));
        writer.WriteEndArray();
        writer.WriteEndObject();
        Assert.Throws<InvalidOperationException>(() => writer.WriteStartObject());

        Assert.Equal("{\n  \"list\": []\n}\n", text.ToString());
    }

    [Fact]
    public void IsCompleteOnceTheRootValueIsClosed()
    {
        using var writer = new JsonWriter(new StringWriter(CultureInfo.InvariantCulture));

        Assert.False(writer.IsComplete);
        writer.WriteStartArray();
        Assert.Equal(1, writer.Depth);
        Assert.False(writer.IsComplete);
        writer.WriteEndArray();
        Assert.True(writer.IsComplete);
    }

    private static string WriteSampleNumbers() => Write(writer =>
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(-1234.5);
        writer.WriteNumberValue(0.25f);
        writer.WriteNumberValue(1e-7);
        writer.WriteNumberValue(-1_000_000);
        writer.WriteEndArray();
    });

    // A JSON \uXXXX escape as text.
    private static string Escaped(string hex) => "\\u" + hex;

    private static string Write(Action<JsonWriter> write)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = new JsonWriter(text, leaveOpen: true))
        {
            write(writer);
        }

        return text.ToString();
    }
}
