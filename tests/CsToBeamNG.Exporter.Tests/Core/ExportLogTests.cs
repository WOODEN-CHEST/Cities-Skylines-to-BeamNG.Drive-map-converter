using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed partial class ExportLogTests
{
    [Fact]
    public void FormatsLinesWithTheUtcTimeLevelAndMessage()
    {
        var time = new DateTime(2026, 11, 7, 9, 5, 3, 42, DateTimeKind.Utc);

        Assert.Equal("2026-11-07T09:05:03.042Z info    Started", ExportLog.FormatLine(time, ExportLogLevel.Info, "Started"));
        Assert.Equal("2026-11-07T09:05:03.042Z detail  x", ExportLog.FormatLine(time, ExportLogLevel.Detail, "x"));
        Assert.Equal("2026-11-07T09:05:03.042Z warning x", ExportLog.FormatLine(time, ExportLogLevel.Warning, "x"));
        Assert.Equal("2026-11-07T09:05:03.042Z error   x", ExportLog.FormatLine(time, ExportLogLevel.Error, "x"));
    }

    [Theory]
    [InlineData("lv-LV")]
    [InlineData("ar-SA")]
    public void FormatsTheTimeTheSameInEveryCulture(string culture)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(
            "2026-11-07T09:05:03.042Z info    x",
            ExportLog.FormatLine(new DateTime(2026, 11, 7, 9, 5, 3, 42, DateTimeKind.Utc), ExportLogLevel.Info, "x"));
    }

    [Fact]
    public void WritesUtf8LinesAndPassesOnAllButDetails()
    {
        using var folder = new TempFolder();
        var forwarded = new List<string>();
        using (ExportLog log = ExportLog.Create(folder.Root, (level, message) => forwarded.Add(level + ": " + message)))
        {
            log.Detail("detail line");
            log.Info("Exporting \"Ålesund\"");
            log.Warning("a warning");
            log.Error("It failed.", new InvalidOperationException("Bad state"));
        }

        byte[] bytes = File.ReadAllBytes(Path.Join(folder.Root, ExportLog.FileName));
        Assert.NotEqual(0xEF, bytes[0]);
        string text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        string[] lines = text.Split('\n');
        Assert.Matches(LinePattern(), lines[0]);
        Assert.EndsWith(" detail  detail line", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(" info    Exporting \"Ålesund\"", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(" warning a warning", lines[2], StringComparison.Ordinal);
        Assert.EndsWith(" error   It failed.", lines[3], StringComparison.Ordinal);
        Assert.StartsWith("System.InvalidOperationException: Bad state", lines[4], StringComparison.Ordinal);
        Assert.Equal(
            ["Info: Exporting \"Ålesund\"", "Warning: a warning", "Error: It failed. InvalidOperationException: Bad state"],
            forwarded);
    }

    [Fact]
    public void KeepsPassingLinesOnAfterTheFileIsClosed()
    {
        using var folder = new TempFolder();
        var forwarded = new List<string>();
        ExportLog log = ExportLog.Create(folder.Root, (_, message) => forwarded.Add(message));
        log.Info("before");
        log.Dispose();

        log.Info("after");
        log.Dispose();

        Assert.Equal(["before", "after"], forwarded);
        Assert.EndsWith(" before\n", File.ReadAllText(Path.Join(folder.Root, ExportLog.FileName)), StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacesAnExistingLog()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Join(folder.Root, ExportLog.FileName), "old content\n");

        using (ExportLog log = ExportLog.Create(folder.Root, null))
        {
            log.Info("new");
        }

        Assert.DoesNotContain("old content", File.ReadAllText(Path.Join(folder.Root, ExportLog.FileName)), StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsLinesWholeWhenThreadsWriteAtOnce()
    {
        using var folder = new TempFolder();
        using (ExportLog log = ExportLog.Create(folder.Root, null))
        {
            Parallel.For(0, 400, i => log.Detail("line " + i));
        }

        string[] lines = File.ReadAllText(Path.Join(folder.Root, ExportLog.FileName)).TrimEnd('\n').Split('\n');
        Assert.Equal(400, lines.Length);
        Assert.All(lines, line => Assert.Matches(LinePattern(), line));
        var numbers = new HashSet<string>();
        foreach (string line in lines)
        {
            numbers.Add(line[(line.LastIndexOf(' ') + 1)..]);
        }

        Assert.Equal(400, numbers.Count);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z (detail |info   |warning|error  ) .+$")]
    private static partial Regex LinePattern();
}
