using System;
using System.Collections.Generic;

namespace CsToBeamNG.Dump.Manifest;

/// <summary>
/// The contents of <c>manifest.json</c>: where a dump came from and what it contains (docs/dump-format.md,
/// manifest.json).
/// </summary>
/// <remarks>
/// The exporter fills it in and writes it with <see cref="ManifestWriter"/>, usually through
/// <see cref="DumpWriter"/>. On .NET 10, <c>ManifestReader</c> reads it. Collections are never null;
/// optional values are null when absent.
/// </remarks>
public sealed class DumpManifest
{
    /// <summary>
    /// The format version: <see cref="DumpFormat.CurrentVersion"/> for a new manifest, or the version
    /// read from the file.
    /// </summary>
    public DumpVersion FormatVersion { get; internal set; } = DumpFormat.CurrentVersion;

    /// <summary>
    /// Whether the export finished. It is <see langword="false"/> while the export runs and if it failed;
    /// readers reject such dumps unless asked not to.
    /// </summary>
    public bool Complete { get; set; }

    /// <summary>When the export started, in UTC (<see cref="DateTimeKind.Utc"/>). It is stored to the second.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>The program that wrote the dump.</summary>
    public ExporterInfo Exporter { get; set; } = new();

    /// <summary>The game the dump was exported from.</summary>
    public GameInfo Game { get; set; } = new();

    /// <summary>The exported city.</summary>
    public CityInfo City { get; set; } = new();

    /// <summary>
    /// The numbers of objects in the game by kind, such as <c>buildings</c>. Keys are camelCase; values
    /// are 0 or more. Sorted by key, so the output is deterministic.
    /// </summary>
    public IDictionary<string, long> Counts { get; } = new SortedDictionary<string, long>(StringComparer.Ordinal);

    /// <summary>
    /// The numbers of objects the exporter skipped because of errors, by kind, with keys like those of
    /// <see cref="Counts"/>. Sorted by key.
    /// </summary>
    public IDictionary<string, long> FailureCounts { get; } = new SortedDictionary<string, long>(StringComparer.Ordinal);

    /// <summary>Short messages for the user about problems in the export. Details are in <c>export.log</c>.</summary>
    public IList<string> Warnings { get; } = new List<string>();

    /// <summary>The sections, in the order they were written. Names are unique.</summary>
    public IList<SectionInfo> Sections { get; } = new List<SectionInfo>();
}
