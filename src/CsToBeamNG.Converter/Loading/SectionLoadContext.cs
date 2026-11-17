using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Converter.Loading;

/// <summary>
/// What a section loader gets besides its section: the manifest, the data loaded before it, and a place to report
/// problems it works around.
/// </summary>
/// <remarks>
/// A loader degrades gracefully on individual objects: it skips a damaged object, reports it with its identity
/// through <see cref="Warn"/>, and counts it in its statistics (agents/conventions.md, Errors, logging,
/// diagnostics). Damage that makes the whole section unusable is a <see cref="Dump.DumpException"/> instead.
/// </remarks>
public sealed class SectionLoadContext
{
    private readonly IReadOnlyList<ISectionData> _loaded;
    private readonly List<LoadMessage> _messages = [];

    internal SectionLoadContext(DumpManifest manifest, string sectionName, IReadOnlyList<ISectionData> loaded)
    {
        Manifest = manifest;
        SectionName = sectionName;
        _loaded = loaded;
    }

    /// <summary>The dump's manifest.</summary>
    public DumpManifest Manifest { get; }

    /// <summary>The name of the section being loaded.</summary>
    public string SectionName { get; }

    /// <summary>What was reported so far, in order.</summary>
    internal IReadOnlyList<LoadMessage> Messages => _messages;

    /// <summary>
    /// Returns the data of a section loaded before this one, or <see langword="null"/> if it wasn't loaded. Loaders
    /// run in registration order (<see cref="SectionLoaderRegistry"/>).
    /// </summary>
    /// <typeparam name="T">The section data type.</typeparam>
    public T? Find<T>()
        where T : class, ISectionData
    {
        foreach (ISectionData data in _loaded)
        {
            if (data is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Reports a problem the loader worked around, such as an object it skipped.</summary>
    /// <param name="message">A message for the user that names the object, such as its id and prefab.</param>
    public void Warn(string message) => Add(LoadMessageSeverity.Warning, message);

    /// <summary>Reports something worth knowing that isn't a problem.</summary>
    /// <param name="message">A message for the user.</param>
    public void Inform(string message) => Add(LoadMessageSeverity.Info, message);

    private void Add(LoadMessageSeverity severity, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        _messages.Add(new LoadMessage(severity, message, SectionName));
    }
}
