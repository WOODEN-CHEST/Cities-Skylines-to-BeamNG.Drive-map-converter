using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Converter.Loading;

/// <summary>
/// The converter's first two stages: opens a dump, runs the section loaders and builds the world model
/// (docs/architecture.md, Converter stages).
/// </summary>
/// <remarks>
/// <para>
/// Whatever stops the whole dump from being read, such as a missing manifest or another major format version, is
/// a <see cref="DumpException"/>. Problems with single sections only skip them, following the reader rules of
/// docs/dump-format.md, Versioning:
/// </para>
/// <list type="bullet">
/// <item><description>a section no loader knows is skipped with an information message;</description></item>
/// <item><description>a section of another major version is skipped with a warning;</description></item>
/// <item><description>a section of a newer minor version is read, with an information message;</description></item>
/// <item><description>a section its loader finds damaged is skipped with an error;</description></item>
/// <item><description>a section that a loader reads but the dump lacks is fine, and listed in <see cref="WorldLoadResult.MissingSections"/>.</description></item>
/// </list>
/// </remarks>
public static class WorldLoader
{
    /// <summary>Opens a dump and loads it.</summary>
    /// <param name="dumpFolder">The dump folder, which holds <c>manifest.json</c>.</param>
    /// <param name="options">Options, or <see langword="null"/> for <see cref="WorldLoadOptions.Default"/>.</param>
    /// <exception cref="DumpException">The dump can't be read at all; the message says why.</exception>
    public static WorldLoadResult Load(string dumpFolder, WorldLoadOptions? options = null)
    {
        options ??= WorldLoadOptions.Default;
        DumpReader reader = DumpReader.Open(dumpFolder, new DumpReadOptions { AllowIncomplete = options.AllowIncomplete });
        return Load(reader, options.Loaders ?? SectionLoaderRegistry.CreateDefault());
    }

    /// <summary>Loads an opened dump with the given loaders.</summary>
    /// <param name="reader">The opened dump.</param>
    /// <param name="loaders">The loaders, which run in their registration order.</param>
    public static WorldLoadResult Load(DumpReader reader, SectionLoaderRegistry loaders)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(loaders);
        var loaded = new List<ISectionData>();
        var outcomes = new Dictionary<string, SectionLoadResult>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (ISectionLoader loader in loaders.Loaders)
        {
            DumpSection? section = reader.FindSection(loader.SectionName);
            if (section is null)
            {
                missing.Add(loader.SectionName);
                continue;
            }

            outcomes.Add(section.Name, LoadSection(reader, section, loader, loaded));
        }

        var messages = new List<LoadMessage>();
        foreach (DumpNotice notice in reader.Notices)
        {
            LoadMessageSeverity severity = notice.Severity == DumpNoticeSeverity.Warning ? LoadMessageSeverity.Warning : LoadMessageSeverity.Info;
            messages.Add(new LoadMessage(severity, notice.Message));
        }

        var results = new List<SectionLoadResult>(reader.Sections.Count);
        foreach (DumpSection section in reader.Sections)
        {
            SectionLoadResult result = outcomes.GetValueOrDefault(section.Name) ?? Unknown(section);
            results.Add(result);
            messages.AddRange(result.Messages);
        }

        var world = new CityWorld(reader.Folder, reader.Manifest, loaded);
        return new WorldLoadResult(world, results, missing, messages);
    }

    private static SectionLoadResult LoadSection(DumpReader reader, DumpSection section, ISectionLoader loader, List<ISectionData> loaded)
    {
        if (!loader.Version.CanRead(section.Version))
        {
            string remedy = section.Version.Major > loader.Version.Major
                ? "Update CsToBeamNG to convert it."
                : "Export the city again with the current exporter.";
            var incompatible = new LoadMessage(
                LoadMessageSeverity.Warning,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Section '{section.Name}' has version {section.Version}, but this version of CsToBeamNG reads only versions {loader.Version.Major}.x, so it is skipped. {remedy}"),
                section.Name);
            return new SectionLoadResult(section, SectionLoadStatus.Incompatible, loader.Version, null, [incompatible]);
        }

        var context = new SectionLoadContext(reader.Manifest, section.Name, loaded);
        if (section.Version.Minor > loader.Version.Minor)
        {
            context.Inform(string.Create(
                CultureInfo.InvariantCulture,
                $"Section '{section.Name}' has version {section.Version}, newer than the {loader.Version} that this version of CsToBeamNG knows. Data added since is ignored."));
        }

        try
        {
            ISectionData data = loader.Load(section, context)
                ?? throw new InvalidOperationException($"The loader of section '{section.Name}' returned no data.");
            loaded.Add(data);
            return new SectionLoadResult(section, SectionLoadStatus.Loaded, loader.Version, data, [.. context.Messages]);
        }
        catch (Exception e) when (e is DumpException or IOException or UnauthorizedAccessException)
        {
            var failed = new LoadMessage(
                LoadMessageSeverity.Error,
                $"Section '{section.Name}' couldn't be read, so it is skipped: {e.Message}",
                section.Name);
            return new SectionLoadResult(section, SectionLoadStatus.Failed, loader.Version, null, [.. context.Messages, failed]);
        }
    }

    private static SectionLoadResult Unknown(DumpSection section)
    {
        var unknown = new LoadMessage(
            LoadMessageSeverity.Info,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Section '{section.Name}' (version {section.Version}) is unknown to this version of CsToBeamNG, so it is skipped."),
            section.Name);
        return new SectionLoadResult(section, SectionLoadStatus.Unknown, null, null, [unknown]);
    }
}
