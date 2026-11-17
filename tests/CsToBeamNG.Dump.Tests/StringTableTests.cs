using System.Globalization;
using System.IO;
using System.Text;
using CsToBeamNG.Dump.Json;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class StringTableTests
{
    [Fact]
    public void BuilderReservesIndexZeroAndStoresEachStringOnce()
    {
        var builder = new StringTableBuilder();

        Assert.Equal(0u, builder.Add(null));
        Assert.Equal(0u, builder.Add(string.Empty));
        Assert.Equal(1u, builder.Add("Oak"));
        Assert.Equal(2u, builder.Add("Basic Road"));
        Assert.Equal(1u, builder.Add("Oak"));
        Assert.Equal(3u, builder.Add("oak"));
        Assert.Equal(4, builder.Count);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var builder = new StringTableBuilder();
        builder.Add("Oak");
        builder.Add("Rīga \"Old Town\"");
        builder.Add("tab\there");

        string json = WriteToString(builder);
        StringTable table = StringTable.Parse(Encoding.UTF8.GetBytes(json), "strings.json");

        Assert.Equal("{\n  \"strings\": [\n    \"\",\n    \"Oak\",\n    \"Rīga \\\"Old Town\\\"\",\n    \"tab\\there\"\n  ]\n}\n", json);
        Assert.Equal(["", "Oak", "Rīga \"Old Town\"", "tab\there"], table.Strings);
        Assert.Equal(string.Empty, table.Get(0));
        Assert.Equal("Oak", table.Get(1));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"strings\": \"\"}")]
    [InlineData("{\"strings\": []}")]
    [InlineData("{\"strings\": [\"Oak\"]}")]
    [InlineData("{\"strings\": [\"\", 3]}")]
    public void RejectsDamagedTables(string json)
    {
        DumpException error = Assert.Throws<DumpException>(() => StringTable.Parse(Encoding.UTF8.GetBytes(json), "strings.json"));

        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
    }

    [Fact]
    public void ReportsIndicesBeyondTheTable()
    {
        StringTable table = StringTable.Parse("{\"strings\": [\"\", \"Oak\"]}"u8.ToArray(), "strings.json");

        Assert.True(table.TryGet(1, out string? oak));
        Assert.Equal("Oak", oak);
        Assert.False(table.TryGet(2, out _));
        DumpException error = Assert.Throws<DumpException>(() => table.Get(uint.MaxValue));
        Assert.Equal(DumpErrorKind.Corrupt, error.Kind);
        Assert.Contains("strings.json", error.Message, System.StringComparison.Ordinal);
    }

    private static string WriteToString(StringTableBuilder builder)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = new JsonWriter(text, leaveOpen: true))
        {
            builder.WriteTo(writer);
        }

        return text.ToString();
    }
}
