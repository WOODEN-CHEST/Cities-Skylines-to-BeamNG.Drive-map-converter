using System;
using System.Linq;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Discovery;

public sealed class KeyValuesParserTests
{
    [Fact]
    public void ParsesNestedSectionsOfTheCurrentLibraryList()
    {
        const string Text = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"/home/user/.local/share/Steam"
            		"label"		""
            		"apps"
            		{
            			"255710"		"17141342788"
            			"284160"		"56074500701"
            		}
            	}
            	"1"
            	{
            		"path"		"/mnt/games/SteamLibrary"
            	}
            }
            """;

        KeyValuesNode document = KeyValuesParser.Parse(Text);

        Assert.Equal(["/home/user/.local/share/Steam", "/mnt/games/SteamLibrary"], SteamLocator.ReadLibraryPaths(document));
        KeyValuesNode? apps = document.Find("libraryfolders")?.Find("0")?.Find("apps");
        Assert.Equal("56074500701", apps?.FindValue("284160"));
        Assert.Equal(string.Empty, document.Find("libraryfolders")?.Find("0")?.FindValue("label"));
    }

    [Fact]
    public void ReadsLibraryPathsInTheOldFormat()
    {
        const string Text = """
            "LibraryFolders"
            {
            	"TimeNextStatsReport"		"1693000000"
            	"ContentStatsID"		"-7012345678901234567"
            	"1"		"D:\\SteamLibrary"
            	"2"		"E:\\Games\\Steam"
            }
            """;

        Assert.Equal([@"D:\SteamLibrary", @"E:\Games\Steam"], SteamLocator.ReadLibraryPaths(KeyValuesParser.Parse(Text)));
    }

    [Fact]
    public void FindsKeysCaseInsensitively()
    {
        KeyValuesNode document = KeyValuesParser.Parse("\"AppState\" { \"installdir\" \"Cities_Skylines\" }");

        Assert.Equal("Cities_Skylines", document.Find("appstate")?.FindValue("InstallDir"));
    }

    [Fact]
    public void UnescapesQuotedStrings()
    {
        KeyValuesNode document = KeyValuesParser.Parse("\"key\" \"a\\\\b \\\"quoted\\\" tab\\tnewline\\n\"");

        Assert.Equal("a\\b \"quoted\" tab\tnewline\n", document.FindValue("key"));
    }

    [Fact]
    public void KeepsTheBackslashOfAnUnknownEscape()
    {
        // Steam escapes backslashes, but a hand-edited file may not; the path must survive.
        KeyValuesNode document = KeyValuesParser.Parse("\"path\" \"C:\\Games\\Steam\"");

        Assert.Equal(@"C:\Games\Steam", document.FindValue("path"));
    }

    [Fact]
    public void IgnoresCommentsOddWhitespaceAndAByteOrderMark()
    {
        const string Text = "\uFEFF// written by hand\r\n\r\n  \"root\"\t// the root\r\n{\r\n\t\t\"key\"   \t\"value\"   // trailing\r\n\r\n}";

        KeyValuesNode document = KeyValuesParser.Parse(Text);

        KeyValuesNode root = Assert.Single(document.Children);
        Assert.Equal("root", root.Key);
        Assert.Equal("value", root.FindValue("key"));
    }

    [Fact]
    public void AcceptsUnquotedTokensAndSkipsConditions()
    {
        KeyValuesNode document = KeyValuesParser.Parse("root { key value [$WIN32] section [$LINUX] { inner 1 } }");

        KeyValuesNode? root = document.Find("root");
        Assert.Equal("value", root?.FindValue("key"));
        Assert.Equal("1", root?.Find("section")?.FindValue("inner"));
    }

    [Fact]
    public void KeepsDuplicateKeysInOrder()
    {
        KeyValuesNode document = KeyValuesParser.Parse("\"a\" \"1\" \"A\" \"2\" \"a\" \"3\"");

        Assert.Equal(["1", "2", "3"], document.Children.Select(child => child.Value));
        Assert.Equal("1", document.FindValue("a"));
    }

    [Theory]
    [InlineData("\"key\" \"value", "line 1", "not closed")]
    [InlineData("\"section\"\n{\n\"key\" \"value\"\n", "line 4", "missing '}'")]
    [InlineData("\"key\" \"value\"\n}", "line 2", "unexpected '}'")]
    [InlineData("\"section\"\n{\n\"key\"\n}", "line 4", "has no value")]
    [InlineData("{ \"key\" \"value\" }", "line 1", "has no key")]
    [InlineData("\"key\" \"value\" [$WIN32", "line 1", "missing ']'")]
    public void RejectsMalformedTextWithTheLineNumber(string text, string line, string reason)
    {
        FormatException error = Assert.Throws<FormatException>(() => KeyValuesParser.Parse(text));

        Assert.Contains(line, error.Message, StringComparison.Ordinal);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAbsurdNesting()
    {
        string text = string.Concat(Enumerable.Repeat("a {", 100)) + string.Concat(Enumerable.Repeat("}", 100));

        Assert.Throws<FormatException>(() => KeyValuesParser.Parse(text));
    }
}
