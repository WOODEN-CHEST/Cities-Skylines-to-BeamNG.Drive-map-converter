using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Autorun;
using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class AutorunSequenceTests
{
    private static readonly DateTime _created = new(2026, 11, 17, 16, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void LoadsTheSaveOnceTheMenuHasSettledThenExportsAndQuits()
    {
        var host = new FakeHost();
        var sequence = new AutorunSequence(Request(quit: true), host);

        sequence.Start(0);
        sequence.Update(1);
        host.IsMenuIdle = true;
        sequence.Update(2);
        sequence.Update(4.4);
        Assert.Null(host.LoadedSave);

        sequence.Update(4.5);
        Assert.Equal("CityPalma", host.LoadedSave);
        Assert.Equal(AutorunPhase.LoadingSave, sequence.Phase);

        host.IsMenuIdle = false;
        sequence.Update(30);
        host.IsCityLoaded = true;
        sequence.Update(31);
        Assert.Equal(AutorunPhase.Exporting, sequence.Phase);
        Assert.Equal(1, host.ExportsStarted);

        sequence.OnExportFinished(succeeded: true, "Exported", "/dumps/san-palma");
        Assert.Equal(AutorunPhase.Quitting, sequence.Phase);
        Assert.Equal(1, host.QuitCalls);

        Assert.Equal(
            [AutorunState.Accepted, AutorunState.LoadingSave, AutorunState.Exporting, AutorunState.Succeeded],
            host.States);
        AutorunStatus final = host.Statuses[^1];
        Assert.Equal("id-1", final.RequestId);
        Assert.Equal("Exported", final.Message);
        Assert.Equal("/dumps/san-palma", final.DumpFolder);
    }

    [Fact]
    public void WaitsForTheMenuToStayIdle()
    {
        var host = new FakeHost { IsMenuIdle = true };
        var sequence = new AutorunSequence(Request(quit: true), host);
        sequence.Start(0);

        sequence.Update(0);
        sequence.Update(2);
        host.IsMenuIdle = false;
        sequence.Update(2.4);
        host.IsMenuIdle = true;
        sequence.Update(3);
        sequence.Update(5.4);
        Assert.Null(host.LoadedSave);

        sequence.Update(5.5);
        Assert.Equal("CityPalma", host.LoadedSave);
    }

    [Fact]
    public void FailsAndQuitsWhenTheSaveCantBeLoaded()
    {
        var host = new FakeHost { IsMenuIdle = true, LoadProblem = "No such save." };
        var sequence = new AutorunSequence(Request(quit: true), host);
        sequence.Start(0);

        sequence.Update(0);
        sequence.Update(3);

        Assert.Equal(AutorunState.Failed, host.States[^1]);
        Assert.Equal("No such save.", host.Statuses[^1].Message);
        Assert.Equal(AutorunPhase.Quitting, sequence.Phase);
        Assert.Equal(1, host.QuitCalls);
        Assert.Equal(0, host.ExportsStarted);
    }

    [Fact]
    public void FailsWhenTheGameReturnsToTheMenuInsteadOfLoading()
    {
        var host = new FakeHost { IsMenuIdle = true };
        var sequence = new AutorunSequence(Request(quit: true), host);
        sequence.Start(0);
        sequence.Update(0);
        sequence.Update(3);
        host.IsMenuIdle = false;
        sequence.Update(20);

        host.IsMenuIdle = true;
        sequence.Update(21);
        sequence.Update(30.9);
        Assert.Equal(AutorunPhase.LoadingSave, sequence.Phase);

        sequence.Update(31);
        Assert.Equal(AutorunState.Failed, host.States[^1]);
        Assert.Contains("went back to the main menu", host.Statuses[^1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAFailedExport()
    {
        var host = new FakeHost();
        var sequence = ReachExporting(host, quit: true);

        sequence.OnExportFinished(succeeded: false, "The export failed: disk full", "/dumps/partial");

        AutorunStatus final = host.Statuses[^1];
        Assert.Equal(AutorunState.Failed, final.State);
        Assert.Equal("The export failed: disk full", final.Message);
        Assert.Equal("/dumps/partial", final.DumpFolder);
        Assert.Equal(1, host.QuitCalls);
    }

    [Fact]
    public void KeepsTheGameRunningWhenAsked()
    {
        var host = new FakeHost();
        var sequence = ReachExporting(host, quit: false);

        sequence.OnExportFinished(succeeded: true, "Exported", "/dumps/x");
        sequence.Update(1000);

        Assert.Equal(AutorunPhase.Done, sequence.Phase);
        Assert.Equal(0, host.QuitCalls);
        Assert.Equal(0, host.ForceQuitCalls);
    }

    [Fact]
    public void ForcesTheGameToQuitWhenItDoesntQuitByItself()
    {
        var host = new FakeHost();
        var sequence = ReachExporting(host, quit: true);
        sequence.Update(100);
        sequence.OnExportFinished(succeeded: true, "Exported", "/dumps/x");

        sequence.Update(159);
        Assert.Equal(0, host.ForceQuitCalls);

        sequence.Update(160);
        Assert.Equal(1, host.ForceQuitCalls);
        Assert.Equal(AutorunPhase.Done, sequence.Phase);

        sequence.Update(500);
        Assert.Equal(1, host.ForceQuitCalls);
    }

    [Fact]
    public void FailsOnceForAnErrorAndThenOnlyForcesTheQuit()
    {
        var host = new FakeHost();
        var sequence = new AutorunSequence(Request(quit: true), host);
        sequence.Start(0);

        sequence.Fail("Something broke.");
        Assert.Equal(AutorunState.Failed, host.States[^1]);
        Assert.Equal(AutorunPhase.Quitting, sequence.Phase);

        sequence.Fail("Quitting broke too.");
        Assert.Equal(1, host.ForceQuitCalls);
        Assert.Equal(AutorunPhase.Done, sequence.Phase);
        Assert.Equal(2, host.Statuses.Count);
    }

    [Fact]
    public void IgnoresAnExportThatEndsInTheWrongPhase()
    {
        var host = new FakeHost();
        var sequence = new AutorunSequence(Request(quit: true), host);
        sequence.Start(0);

        sequence.OnExportFinished(succeeded: true, "Exported", "/dumps/x");

        Assert.Equal(AutorunPhase.WaitingForMenu, sequence.Phase);
        Assert.Single(host.Statuses);
        Assert.Single(host.Warnings);
    }

    private static AutorunRequest Request(bool quit) => new("id-1", _created, "CityPalma", quit);

    private static AutorunSequence ReachExporting(FakeHost host, bool quit)
    {
        var sequence = new AutorunSequence(Request(quit), host);
        sequence.Start(0);
        host.IsMenuIdle = true;
        sequence.Update(0);
        sequence.Update(3);
        host.IsMenuIdle = false;
        host.IsCityLoaded = true;
        sequence.Update(4);
        Assert.Equal(AutorunPhase.Exporting, sequence.Phase);
        return sequence;
    }

    private sealed class FakeHost : IAutorunHost
    {
        public bool IsMenuIdle { get; set; }

        public bool IsCityLoaded { get; set; }

        public string? LoadProblem { get; set; }

        public string? LoadedSave { get; private set; }

        public int ExportsStarted { get; private set; }

        public int QuitCalls { get; private set; }

        public int ForceQuitCalls { get; private set; }

        public List<AutorunStatus> Statuses { get; } = [];

        public List<AutorunState> States => Statuses.ConvertAll(status => status.State);

        public List<string> Warnings { get; } = [];

        public string? StartLoadingSave(string saveName)
        {
            if (LoadProblem is null)
            {
                LoadedSave = saveName;
            }

            return LoadProblem;
        }

        public void StartExport() => ExportsStarted++;

        public void ReportStatus(AutorunStatus status) => Statuses.Add(status);

        public void Quit() => QuitCalls++;

        public void ForceQuit() => ForceQuitCalls++;

        public void Log(string message)
        {
        }

        public void Warn(string message) => Warnings.Add(message);
    }
}
