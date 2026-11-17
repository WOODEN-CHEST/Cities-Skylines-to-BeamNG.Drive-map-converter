#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Dump;

/// <summary>
/// Opens a dump folder: reads and checks <c>manifest.json</c>, checks that every listed file exists with the
/// listed size, and gives access to the sections (docs/dump-format.md). Only in the .NET 10 build.
/// </summary>
/// <remarks>
/// It fails fast with a <see cref="DumpException"/> whose <see cref="DumpException.Kind"/> says why: the
/// dump is missing, damaged, of an incompatible format version, or from an export that didn't finish.
/// What doesn't stop reading is reported in <see cref="Notices"/>. It doesn't judge sections: the converter
/// skips sections it doesn't know or whose major version it doesn't read.
/// </remarks>
public sealed class DumpReader
{
    private readonly Dictionary<string, DumpSection> _sectionsByName = new(StringComparer.Ordinal);

    private DumpReader(string folder, DumpManifest manifest, DumpSection[] sections, DumpNotice[] notices)
    {
        Folder = folder;
        Manifest = manifest;
        Sections = sections;
        Notices = notices;
        foreach (DumpSection section in sections)
        {
            _sectionsByName.Add(section.Name, section);
        }
    }

    /// <summary>The dump folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>The manifest.</summary>
    public DumpManifest Manifest { get; }

    /// <summary>The dump's format version.</summary>
    public DumpVersion FormatVersion => Manifest.FormatVersion;

    /// <summary>The sections, in manifest order.</summary>
    public IReadOnlyList<DumpSection> Sections { get; }

    /// <summary>What was noticed while opening the dump, for the user or the log.</summary>
    public IReadOnlyList<DumpNotice> Notices { get; }

    /// <summary>Opens a dump.</summary>
    /// <param name="folder">The dump folder, which holds <c>manifest.json</c>.</param>
    /// <param name="options">Options, or <see langword="null"/> for <see cref="DumpReadOptions.Default"/>.</param>
    /// <exception cref="DumpException">The dump can't be read; the message says why.</exception>
    public static DumpReader Open(string folder, DumpReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        options ??= DumpReadOptions.Default;
        string fullFolder = Path.GetFullPath(folder);
        if (!Directory.Exists(fullFolder))
        {
            throw new DumpException(
                DumpErrorKind.NotFound,
                string.Create(CultureInfo.InvariantCulture, $"There is no dump at '{fullFolder}': the folder doesn't exist."));
        }

        string manifestPath = Path.Join(fullFolder, DumpFormat.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            string hint = File.Exists(manifestPath + ".tmp")
                ? "The export was probably interrupted while writing it."
                : "A dump folder holds a manifest.json and one folder per section.";
            throw new DumpException(
                DumpErrorKind.NotFound,
                string.Create(CultureInfo.InvariantCulture, $"'{fullFolder}' is not a city dump: it has no {DumpFormat.ManifestFileName}. {hint}"));
        }

        DumpManifest manifest = ManifestReader.ReadFile(manifestPath);
        var notices = new List<DumpNotice>();
        if (manifest.FormatVersion.Minor > DumpFormat.MinorVersion)
        {
            notices.Add(new DumpNotice(
                DumpNoticeSeverity.Info,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The dump has format version {manifest.FormatVersion}, newer than the {DumpFormat.CurrentVersion} that this version of CsToBeamNG knows. Data added since is ignored.")));
        }

        if (!manifest.Complete)
        {
            string message = string.Create(
                CultureInfo.InvariantCulture,
                $"The export that wrote '{fullFolder}' didn't finish (complete: false in {DumpFormat.ManifestFileName}), so the dump may lack data.");
            if (!options.AllowIncomplete)
            {
                throw new DumpException(DumpErrorKind.Incomplete, message + " Export the city again.");
            }

            notices.Add(new DumpNotice(DumpNoticeSeverity.Warning, message));
        }

        var sections = new DumpSection[manifest.Sections.Count];
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i] = new DumpSection(fullFolder, manifest.Sections[i]);
            sections[i].CheckFiles();
        }

        return new DumpReader(fullFolder, manifest, sections, [.. notices]);
    }

    /// <summary>Returns a section, or <see langword="null"/> if the dump doesn't have it.</summary>
    /// <param name="name">The section's name.</param>
    public DumpSection? FindSection(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _sectionsByName.GetValueOrDefault(name);
    }
}
#endif
