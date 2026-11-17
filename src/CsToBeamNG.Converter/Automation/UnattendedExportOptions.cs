using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.Automation;

/// <summary>What <see cref="UnattendedExport"/> exports and how long it waits.</summary>
public sealed record UnattendedExportOptions
{
    /// <summary>The default for <see cref="Timeout"/>: generous, because big cities with many assets load slowly.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    /// <summary>The game's user-data folder, which holds the autorun folder.</summary>
    public required string UserDataDir { get; init; }

    /// <summary>The save to export, as the game's Load panel lists it.</summary>
    public required string SaveName { get; init; }

    /// <summary>How long to wait for the export, from the game's start.</summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Whether to leave the game running after the export, instead of quitting it.</summary>
    public bool KeepRunning { get; init; }

    /// <summary>The game's log file, named in messages about failures, if known.</summary>
    public string? GameLogPath { get; init; }

    /// <summary>
    /// Developer probes to run with the export (<c>CsToBeamNG.Dump.DumpProbes</c>); empty for a normal
    /// export. The exporter writes them into the dump's <c>probes</c> folder.
    /// </summary>
    public IReadOnlyList<string> Probes { get; init; } = [];
}
