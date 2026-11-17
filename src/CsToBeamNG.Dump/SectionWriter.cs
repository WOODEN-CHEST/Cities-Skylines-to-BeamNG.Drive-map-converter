using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump;

/// <summary>
/// Writes the files of one section of a dump and, when it is complete, adds the section to the manifest.
/// Created by <see cref="DumpWriter.BeginSection"/>.
/// </summary>
/// <remarks>
/// Every file is created through this writer, so the manifest lists it. Finish and dispose the writers it
/// returns before calling <see cref="Complete"/>, which checks that and records each file's size.
/// Not thread-safe.
/// </remarks>
public sealed class SectionWriter
{
    private readonly DumpWriter _dump;
    private readonly List<TrackedFile> _files = [];
    private readonly Dictionary<string, bool> _paths = new(StringComparer.Ordinal);
    private bool _completed;

    internal SectionWriter(DumpWriter dump, string name, DumpVersion version, string folder)
    {
        _dump = dump;
        Name = name;
        Version = version;
        Folder = folder;
    }

    /// <summary>The section's name, which is also its folder's name.</summary>
    public string Name { get; }

    /// <summary>The version of the section's content.</summary>
    public DumpVersion Version { get; }

    /// <summary>The section's folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>
    /// The number of items the section describes, as its specification defines them, or
    /// <see langword="null"/>. Set it before <see cref="Complete"/>.
    /// </summary>
    public long? RecordCount { get; set; }

    /// <summary>Creates a record file in the section.</summary>
    /// <param name="path">The path within the section's folder, such as <c>nodes.bin</c> (docs/dump-format.md, Names).</param>
    /// <param name="recordType">The record type name, such as <c>netNode</c>.</param>
    /// <param name="recordSize">The size of each record in bytes.</param>
    /// <returns>The writer. Call <see cref="RecordFileWriter.Finish"/> and dispose it before <see cref="Complete"/>.</returns>
    public RecordFileWriter CreateRecordFile(string path, string recordType, int recordSize)
    {
        string fullPath = PrepareFile(path);
        RecordFileWriter writer = RecordFileWriter.Create(fullPath, recordType, recordSize);
        Track(new TrackedFile(path, fullPath, writer, null));
        return writer;
    }

    /// <summary>Creates a JSON file in the section.</summary>
    /// <param name="path">The path within the section's folder, such as <c>prefabs.json</c> (docs/dump-format.md, Names).</param>
    /// <returns>The writer. Write one complete document and dispose it before <see cref="Complete"/>.</returns>
    public JsonWriter CreateJsonFile(string path)
    {
        string fullPath = PrepareFile(path);
        JsonWriter writer = JsonWriter.CreateFile(fullPath);
        Track(new TrackedFile(path, fullPath, null, writer));
        return writer;
    }

    /// <summary>Writes a string table file (docs/dump-format.md, String tables).</summary>
    /// <param name="path">The path within the section's folder, conventionally <c>strings.json</c>.</param>
    /// <param name="strings">The strings.</param>
    public void WriteStringTable(string path, StringTableBuilder strings)
    {
        Guard.NotNull(strings, nameof(strings));
        using JsonWriter writer = CreateJsonFile(path);
        strings.WriteTo(writer);
    }

    /// <summary>Records the size of every file and adds the section to the dump's manifest.</summary>
    /// <exception cref="InvalidOperationException">
    /// A file's writer isn't finished or disposed, or the section is already complete.
    /// </exception>
    public void Complete()
    {
        ThrowIfCompleted();
        foreach (TrackedFile file in _files)
        {
            if (file.Records is not null && !file.Records.IsFinished)
            {
                throw NotClosed(file, "isn't finished; call Finish on its writer");
            }

            if (file.Json is not null && !file.Json.IsComplete)
            {
                throw NotClosed(file, "doesn't hold a complete JSON document");
            }

            if (file.Records?.IsDisposed == false || file.Json?.IsDisposed == false)
            {
                throw NotClosed(file, "is still open; dispose its writer");
            }
        }

        var section = new SectionInfo { Name = Name, Version = Version, RecordCount = RecordCount };
        foreach (TrackedFile file in _files)
        {
            section.Files.Add(new SectionFileInfo(file.Path, new FileInfo(file.FullPath).Length));
        }

        _dump.AddCompletedSection(section);
        _completed = true;
    }

    private string PrepareFile(string path)
    {
        ThrowIfCompleted();
        Guard.NotNull(path, nameof(path));
        if (!DumpNames.IsValidFilePath(Name, path))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid file path: {1}.", path, DumpNames.FilePathRule),
                nameof(path));
        }

        if (_paths.ContainsKey(path))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Section '{0}' already has a file '{1}'.",
                Name,
                path));
        }

        string fullPath = DumpNames.ToFileSystemPath(Folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }

    private void Track(TrackedFile file)
    {
        _files.Add(file);
        _paths.Add(file.Path, true);
    }

    private InvalidOperationException NotClosed(TrackedFile file, string problem) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "Section '{0}' can't be completed: '{1}' {2}.",
            Name,
            file.Path,
            problem));

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Section '{0}' is complete; nothing more can be added.",
                Name));
        }
    }

    private sealed class TrackedFile
    {
        public TrackedFile(string path, string fullPath, RecordFileWriter? records, JsonWriter? json)
        {
            Path = path;
            FullPath = fullPath;
            Records = records;
            Json = json;
        }

        public string Path { get; }

        public string FullPath { get; }

        public RecordFileWriter? Records { get; }

        public JsonWriter? Json { get; }
    }
}
