#if NET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump;

/// <summary>
/// A section of a dump opened by <see cref="DumpReader"/>: its version and access to its files. Only in the
/// .NET 10 build.
/// </summary>
/// <remarks>
/// Before reading, compare <see cref="Version"/> with the version your loader supports: if the major
/// versions differ (<see cref="DumpVersion.CanRead"/>), skip the section with a warning
/// (docs/dump-format.md, Versioning).
/// </remarks>
public sealed class DumpSection
{
    private readonly Dictionary<string, SectionFileInfo> _files = new(StringComparer.Ordinal);

    internal DumpSection(string dumpFolder, SectionInfo info)
    {
        Name = info.Name;
        Version = info.Version;
        RecordCount = info.RecordCount;
        Folder = Path.Join(dumpFolder, info.Name);
        Files = [.. info.Files];
        foreach (SectionFileInfo file in info.Files)
        {
            _files.Add(file.Path, file);
        }
    }

    /// <summary>The section's name, which is also its folder's name.</summary>
    public string Name { get; }

    /// <summary>The version of the section's content.</summary>
    public DumpVersion Version { get; }

    /// <summary>The number of items the section describes, if the manifest says.</summary>
    public long? RecordCount { get; }

    /// <summary>The section's folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>The section's files, as listed in the manifest.</summary>
    public IReadOnlyList<SectionFileInfo> Files { get; }

    /// <summary>Whether the manifest lists a file for this section.</summary>
    /// <param name="path">The path within the section's folder, such as <c>nodes.bin</c>.</param>
    public bool HasFile(string path) => _files.ContainsKey(path);

    /// <summary>Returns the file-system path of one of the section's files.</summary>
    /// <param name="path">The path within the section's folder, such as <c>nodes.bin</c>.</param>
    /// <exception cref="DumpException">The manifest doesn't list the file for this section.</exception>
    public string GetFilePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!_files.ContainsKey(path))
        {
            throw new DumpException(
                DumpErrorKind.Corrupt,
                string.Create(CultureInfo.InvariantCulture, $"Section '{Name}' in '{Folder}' has no file '{path}': the manifest doesn't list it."));
        }

        return DumpNames.ToFileSystemPath(Folder, path);
    }

    /// <summary>Opens one of the section's record files and checks that it holds the expected records.</summary>
    /// <param name="path">The path within the section's folder, such as <c>nodes.bin</c>.</param>
    /// <param name="recordType">The record type the file must hold.</param>
    /// <param name="minimumRecordSize">The size of the layout's first version, which later minor versions only extend.</param>
    /// <exception cref="DumpException">The file isn't listed, is damaged, or holds other records.</exception>
    public RecordFileReader OpenRecordFile(string path, string recordType, int minimumRecordSize) =>
        RecordFileReader.Open(GetFilePath(path), recordType, minimumRecordSize);

    /// <summary>Reads one of the section's string tables.</summary>
    /// <param name="path">The path within the section's folder, conventionally <c>strings.json</c>.</param>
    /// <exception cref="DumpException">The file isn't listed or is damaged.</exception>
    public StringTable ReadStringTable(string path) => StringTable.ReadFile(GetFilePath(path));

    /// <summary>Parses one of the section's JSON files, following the dump's JSON conventions.</summary>
    /// <param name="path">The path within the section's folder, such as <c>prefabs.json</c>.</param>
    /// <returns>The document. Dispose it after use.</returns>
    /// <exception cref="DumpException">The file isn't listed or isn't valid JSON.</exception>
    public JsonDocument ReadJson(string path)
    {
        string fullPath = GetFilePath(path);
        return JsonFieldReader.Parse(JsonFieldReader.ReadFileBytes(fullPath), fullPath);
    }

    /// <summary>Checks that every listed file exists and has the listed size.</summary>
    internal void CheckFiles()
    {
        foreach (SectionFileInfo file in Files)
        {
            string fullPath = DumpNames.ToFileSystemPath(Folder, file.Path);
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                throw new DumpException(
                    DumpErrorKind.Corrupt,
                    string.Create(CultureInfo.InvariantCulture, $"'{fullPath}' is missing, but the manifest lists it in section '{Name}'. The dump is damaged; export the city again."));
            }

            if (info.Length != file.Bytes)
            {
                throw new DumpException(
                    DumpErrorKind.Corrupt,
                    string.Create(CultureInfo.InvariantCulture, $"'{fullPath}' has {info.Length} bytes, but the manifest lists {file.Bytes}. The dump is damaged; export the city again."));
            }
        }
    }
}
#endif
