using System;
using System.IO;
using CsToBeamNG.Dump.Autorun;
using Xunit;

namespace CsToBeamNG.Dump.Tests;

public sealed class AutorunTests
{
    private static readonly DateTime _created = new(2026, 11, 17, 16, 30, 5, DateTimeKind.Utc);

    [Fact]
    public void WritesTheRequestAsKeyValueLines()
    {
        var request = new AutorunRequest("abc123", _created, "CityPalma", quitWhenDone: true);

        Assert.Equal(
            "# CsToBeamNG autorun request\nformat=1\nid=abc123\ncreated=2026-11-17T16:30:05Z\nsave=CityPalma\nquit=true\n",
            request.ToText());
    }

    [Fact]
    public void ReadsTheRequestBack()
    {
        var request = new AutorunRequest("abc123", _created.AddTicks(1234567), "My = City\\2\nTwo lines, Łódź", quitWhenDone: false);

        AutorunRequest read = AutorunRequest.Parse(request.ToText());

        Assert.Equal("abc123", read.Id);
        Assert.Equal(_created, read.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, read.CreatedUtc.Kind);
        Assert.Equal("My = City\\2\nTwo lines, Łódź", read.SaveName);
        Assert.False(read.QuitWhenDone);
    }

    [Fact]
    public void WritesProbesOnlyWhenThereAreSome()
    {
        var request = new AutorunRequest("abc123", _created, "CityPalma", quitWhenDone: true, ["geometry", "other-probe", "geometry"]);

        Assert.Equal(
            "# CsToBeamNG autorun request\nformat=1\nid=abc123\ncreated=2026-11-17T16:30:05Z\nsave=CityPalma\nquit=true\nprobes=geometry,other-probe\n",
            request.ToText());
        Assert.Equal(["geometry", "other-probe"], AutorunRequest.Parse(request.ToText()).Probes);
        Assert.Empty(AutorunRequest.Parse(new AutorunRequest("a", _created, "s", quitWhenDone: true).ToText()).Probes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Geometry")]
    [InlineData("geometry,")]
    [InlineData("1st")]
    public void RejectsInvalidProbeNames(string probes)
    {
        string text = "format=1\nid=x\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=true\nprobes=" + probes + "\n";

        if (probes.Length == 0)
        {
            // An empty value means no probes.
            Assert.Empty(AutorunRequest.Parse(text).Probes);
        }
        else
        {
            Assert.Throws<FormatException>(() => AutorunRequest.Parse(text));
        }

        Assert.Throws<ArgumentException>(() => new AutorunRequest("a", _created, "s", quitWhenDone: true, ["Bad"]));
    }

    [Fact]
    public void WritesTheTimeTheSameWayInEveryCulture()
    {
        using var culture = new CultureScope("lv-LV");
        var request = new AutorunRequest("a", _created, "s", quitWhenDone: true);

        Assert.Contains("created=2026-11-17T16:30:05Z\n", request.ToText(), StringComparison.Ordinal);
        Assert.Equal(_created, AutorunRequest.Parse(request.ToText()).CreatedUtc);
    }

    [Fact]
    public void StoresALocalTimeAsUtc()
    {
        DateTime local = _created.ToLocalTime();

        Assert.Equal(_created, new AutorunRequest("a", local, "s", quitWhenDone: true).CreatedUtc);
    }

    [Fact]
    public void AcceptsCarriageReturnsCommentsAndUnknownKeys()
    {
        string text = "# comment\r\n\r\nformat=1\r\nid=x\r\ncreated=2026-11-17T16:30:05Z\r\nsave=A\r\nquit=true\r\nfuture-key=1\r\n";

        AutorunRequest request = AutorunRequest.Parse(text);

        Assert.Equal("x", request.Id);
        Assert.Equal("A", request.SaveName);
    }

    [Theory]
    [InlineData("format=1\nid=x\ncreated=2026-11-17T16:30:05Z\nquit=true\n", "'save' is missing")]
    [InlineData("format=2\nid=x\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=true\n", "has format 2")]
    [InlineData("format=1\nid=x\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=yes\n", "must be true or false")]
    [InlineData("format=1\nid=x\ncreated=17.11.2026 16:30\nsave=A\nquit=true\n", "is not a time")]
    [InlineData("format=1\nid=x\nid=y\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=true\n", "used twice")]
    [InlineData("format=1\nid=x\ncreated=2026-11-17T16:30:05Z\nsave A\nquit=true\n", "Line 4 has no '='")]
    [InlineData("format=1\nId=x\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=true\n", "not a valid key")]
    [InlineData("format=1\nid=x\ncreated=2026-11-17T16:30:05Z\nsave=A\\t\nquit=true\n", "invalid escape")]
    [InlineData("format=1\nid=\ncreated=2026-11-17T16:30:05Z\nsave=A\nquit=true\n", "must not be empty")]
    public void RejectsInvalidRequests(string text, string expectedMessagePart)
    {
        var exception = Assert.Throws<FormatException>(() => AutorunRequest.Parse(text));

        Assert.Contains(expectedMessagePart, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindsTheIdOfARequestItCantRead()
    {
        Assert.Equal("x", AutorunRequest.TryReadId("format=9\nid=x\nsomething-new=1\n"));
        Assert.Null(AutorunRequest.TryReadId("format=1\nno equals sign\nid=x\n"));
        Assert.Null(AutorunRequest.TryReadId("format=1\nid=\n"));
    }

    [Fact]
    public void ConsidersARequestStaleAfterFifteenMinutes()
    {
        var request = new AutorunRequest("a", _created, "s", quitWhenDone: true);

        Assert.False(request.IsStale(_created.AddMinutes(15)));
        Assert.True(request.IsStale(_created.AddMinutes(15).AddSeconds(1)));
    }

    [Fact]
    public void WritesAndReadsStatuses()
    {
        var status = new AutorunStatus("abc", AutorunState.Succeeded, "Exported\nwith a second line", Path.Join("dumps", "city_20261117-163005"));

        string text = status.ToText();
        AutorunStatus read = AutorunStatus.Parse(text);

        Assert.Equal(
            "# CsToBeamNG autorun status\nformat=1\nrequest-id=abc\nstate=succeeded\nmessage=Exported\\nwith a second line\ndump="
            + Path.Join("dumps", "city_20261117-163005").Replace("\\", "\\\\", StringComparison.Ordinal) + "\n",
            text);
        Assert.Equal("abc", read.RequestId);
        Assert.Equal(AutorunState.Succeeded, read.State);
        Assert.True(read.IsFinal);
        Assert.Equal("Exported\nwith a second line", read.Message);
        Assert.Equal(status.DumpFolder, read.DumpFolder);
    }

    [Theory]
    [InlineData(AutorunState.Accepted, "accepted", false)]
    [InlineData(AutorunState.LoadingSave, "loading-save", false)]
    [InlineData(AutorunState.Exporting, "exporting", false)]
    [InlineData(AutorunState.Succeeded, "succeeded", true)]
    [InlineData(AutorunState.Failed, "failed", true)]
    public void NamesEveryState(AutorunState state, string name, bool isFinal)
    {
        AutorunStatus read = AutorunStatus.Parse(new AutorunStatus(null, state, null, null).ToText());

        Assert.Equal(name, AutorunStatus.GetStateName(state));
        Assert.Equal(state, read.State);
        Assert.Equal(isFinal, read.IsFinal);
        Assert.Null(read.RequestId);
        Assert.Null(read.Message);
        Assert.Null(read.DumpFolder);
    }

    [Fact]
    public void RejectsUnknownStates()
    {
        var exception = Assert.Throws<FormatException>(() => AutorunStatus.Parse("format=1\nrequest-id=a\nstate=paused\n"));

        Assert.Contains("'paused' is not a known state", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PutsTheAutorunFolderNextToTheDumps()
    {
        string userData = Path.Join("home", "Colossal Order", "Cities_Skylines");

        Assert.Equal(Path.Join(userData, "CsToBeamNG", "autorun"), AutorunFiles.GetFolder(userData));
    }

    [Fact]
    public void WritesAndReplacesFilesAsUtf8WithoutByteOrderMark()
    {
        using var temp = new TempFolder();
        string path = temp.PathOf("status.txt");

        Assert.Null(AutorunFiles.ReadText(path));
        AutorunFiles.WriteText(path, "first Łódź\n");
        AutorunFiles.WriteText(path, "second Łódź\n");

        Assert.Equal("second Łódź\n", AutorunFiles.ReadText(path));
        Assert.Equal((byte)'s', File.ReadAllBytes(path)[0]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void MovesOntoAnExistingFileAndDeletesMissingFilesQuietly()
    {
        using var temp = new TempFolder();
        string request = temp.PathOf("request.txt");
        string consumed = temp.PathOf("request.consumed.txt");
        File.WriteAllText(consumed, "old");
        File.WriteAllText(request, "new");

        AutorunFiles.Move(request, consumed);
        AutorunFiles.Delete(request);

        Assert.False(File.Exists(request));
        Assert.Equal("new", File.ReadAllText(consumed));
    }

    [Fact]
    public void ReadsAFileThatAnotherProcessHasOpen()
    {
        using var temp = new TempFolder();
        string path = temp.PathOf("status.txt");
        File.WriteAllText(path, "text");
        using var open = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        Assert.Equal("text", AutorunFiles.ReadText(path));
    }
}
