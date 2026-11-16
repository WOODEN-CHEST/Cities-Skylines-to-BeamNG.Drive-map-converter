using System;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>A Cities: Skylines savegame file.</summary>
/// <param name="Name">The name, which is the file name without the <c>.crp</c> extension.</param>
/// <param name="Path">The full path of the file.</param>
/// <param name="SizeBytes">The file size.</param>
/// <param name="LastWriteTimeUtc">When the file was last written, in UTC.</param>
/// <param name="Location">Where the file is stored.</param>
/// <param name="SteamAccountId">For Steam Cloud saves, the Steam account folder they belong to; otherwise <see langword="null"/>.</param>
public sealed record SaveGame(
    string Name,
    string Path,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    SaveLocation Location,
    string? SteamAccountId);
