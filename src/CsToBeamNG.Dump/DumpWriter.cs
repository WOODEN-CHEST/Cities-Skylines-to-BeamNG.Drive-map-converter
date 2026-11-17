using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Dump;

/// <summary>
/// Writes a dump: creates its folder, its sections and <c>manifest.json</c> (docs/dump-format.md). This is
/// what the exporter uses inside the game, on .NET 3.5.
/// </summary>
/// <remarks>
/// <para>Typical use:</para>
/// <list type="number">
/// <item><description>
/// <see cref="Create"/> with a manifest that has the exporter, game and city filled in, and
/// <see cref="DumpManifest.Complete"/> false.
/// </description></item>
/// <item><description>
/// Optionally <see cref="WriteManifest"/>, so that an interrupted export leaves a manifest that says it
/// is incomplete.
/// </description></item>
/// <item><description>
/// For each section, <see cref="BeginSection"/>; create and write its files; dispose their writers; then
/// <see cref="SectionWriter.Complete"/>, which adds the section to the manifest.
/// </description></item>
/// <item><description>Set <see cref="DumpManifest.Complete"/> to true and call <see cref="WriteManifest"/>.</description></item>
/// </list>
/// <para>A section that is never completed is not in the manifest, so readers ignore its files. Not thread-safe.</para>
/// </remarks>
public sealed class DumpWriter
{
    private readonly Dictionary<string, bool> _sectionNames = new(StringComparer.Ordinal);

    private DumpWriter(string folder, DumpManifest manifest)
    {
        Folder = folder;
        Manifest = manifest;
    }

    /// <summary>The dump folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>The manifest that <see cref="WriteManifest"/> writes. Completed sections are added to it.</summary>
    public DumpManifest Manifest { get; }

    /// <summary>Starts a dump in a new or empty folder.</summary>
    /// <param name="folder">The dump folder. It is created if it doesn't exist.</param>
    /// <param name="manifest">The manifest to fill in and write.</param>
    /// <exception cref="IOException">The folder exists and isn't empty, or can't be created.</exception>
    public static DumpWriter Create(string folder, DumpManifest manifest)
    {
        Guard.NotNull(folder, nameof(folder));
        Guard.NotNull(manifest, nameof(manifest));
        string fullFolder = Path.GetFullPath(folder);
        if (Directory.Exists(fullFolder) && Directory.GetFileSystemEntries(fullFolder).Length > 0)
        {
            throw new IOException(string.Format(
                CultureInfo.InvariantCulture,
                "The folder '{0}' isn't empty. A dump needs a new or empty folder.",
                fullFolder));
        }

        Directory.CreateDirectory(fullFolder);
        return new DumpWriter(fullFolder, manifest);
    }

    /// <summary>Starts a section and creates its folder.</summary>
    /// <param name="name">The section's name, such as <c>net-nodes</c> (docs/dump-format.md, Names).</param>
    /// <param name="version">The version of the section's content.</param>
    /// <exception cref="ArgumentException">The name is invalid.</exception>
    /// <exception cref="InvalidOperationException">The dump already has a section with this name.</exception>
    public SectionWriter BeginSection(string name, DumpVersion version)
    {
        Guard.NotNull(name, nameof(name));
        if (!DumpNames.IsValidSectionName(name))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid section name: {1}.", name, DumpNames.SectionNameRule),
                nameof(name));
        }

        if (_sectionNames.ContainsKey(name) || FindSection(name) is not null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "The dump already has a section named '{0}'.",
                name));
        }

        string folder = Path.Combine(Folder, name);
        Directory.CreateDirectory(folder);
        _sectionNames.Add(name, true);
        return new SectionWriter(this, name, version, folder);
    }

    /// <summary>
    /// Writes <c>manifest.json</c> with the sections completed so far, replacing the previous one. Call it
    /// at the start of the export with <see cref="DumpManifest.Complete"/> false, and at the end with true.
    /// </summary>
    /// <exception cref="ArgumentException">The manifest is invalid.</exception>
    public void WriteManifest() => ManifestWriter.WriteFile(Manifest, Path.Combine(Folder, DumpFormat.ManifestFileName));

    internal void AddCompletedSection(SectionInfo section) => Manifest.Sections.Add(section);

    private SectionInfo? FindSection(string name)
    {
        foreach (SectionInfo section in Manifest.Sections)
        {
            if (string.Equals(section.Name, name, StringComparison.Ordinal))
            {
                return section;
            }
        }

        return null;
    }
}
