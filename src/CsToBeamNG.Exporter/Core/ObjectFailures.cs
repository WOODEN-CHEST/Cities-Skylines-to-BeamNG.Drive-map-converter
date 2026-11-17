using System.Collections.Generic;
using System.Globalization;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// Collects the objects of one kind that an export skipped because reading them failed, such as a segment whose corners
/// the game couldn't compute (agents/conventions.md, one bad object must not abort an export). It counts every failure
/// and keeps the first <see cref="MaxDetails"/> descriptions for <c>export.log</c>, so a broken city can't flood the log.
/// </summary>
internal sealed class ObjectFailures
{
    /// <summary>How many failures are described in detail; the rest are only counted.</summary>
    public const int MaxDetails = 20;

    private readonly List<string> _details = new();

    /// <summary>Creates an empty collection.</summary>
    /// <param name="countKey">The manifest's <c>failureCounts</c> key, such as <c>netSegments</c> (docs/dump-format.md, <c>counts</c>).</param>
    /// <param name="description">What the objects are, in plural words, such as <c>network segments</c>.</param>
    public ObjectFailures(string countKey, string description)
    {
        CountKey = countKey;
        Description = description;
    }

    /// <summary>The manifest's <c>failureCounts</c> key.</summary>
    public string CountKey { get; }

    /// <summary>What the objects are, in plural words.</summary>
    public string Description { get; }

    /// <summary>The number of failures.</summary>
    public long Count { get; private set; }

    /// <summary>The descriptions of the first <see cref="MaxDetails"/> failures, in order.</summary>
    public IList<string> Details => _details;

    /// <summary>
    /// A one-line summary for the manifest's warnings, such as <c>2 network segments couldn't be exported, so the dump
    /// lacks them; see export.log.</c>; <see langword="null"/> if nothing failed.
    /// </summary>
    public string? Summary => Count == 0
        ? null
        : string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1} couldn't be exported, so the dump lacks them; see {2}.",
            Count,
            Description,
            ExportLog.FileName);

    /// <summary>Counts a failure.</summary>
    /// <param name="detail">What failed and why, naming the object, such as its id and prefab.</param>
    public void Add(string detail)
    {
        Count++;
        if (_details.Count < MaxDetails)
        {
            _details.Add(detail);
        }
    }
}
