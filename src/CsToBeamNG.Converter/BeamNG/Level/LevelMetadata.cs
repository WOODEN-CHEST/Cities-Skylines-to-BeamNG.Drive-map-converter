using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.BeamNG.Level;

/// <summary>
/// What a level's <c>info.json</c> says about it: the title and description shown in the level list, its size, and
/// its spawn points (docs/research/beamng-ai-and-traffic.md, <c>info.json</c> gameplay keys).
/// </summary>
public sealed class LevelMetadata
{
    private readonly IReadOnlyList<LevelSpawnPoint> _spawnPoints = [];

    /// <summary>The title. The game shows the level's folder name instead while the level has no preview image.</summary>
    public required string Title { get; init; }

    /// <summary>The description, or <see langword="null"/>.</summary>
    public string? Description { get; init; }

    /// <summary>The authors, or <see langword="null"/>.</summary>
    public string? Authors { get; init; }

    /// <summary>The level's extent in metres along x and y, for the level list.</summary>
    public required (int X, int Y) Size { get; init; }

    /// <summary>The spawn points offered in the level list; the first is the default.</summary>
    public IReadOnlyList<LevelSpawnPoint> SpawnPoints
    {
        get => _spawnPoints;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _spawnPoints = [.. value];
        }
    }

    /// <summary>Whether the game offers AI traffic in this level.</summary>
    public bool SupportsTraffic { get; init; } = true;

    /// <summary>Whether the game offers a choice of the time of day.</summary>
    public bool SupportsTimeOfDay { get; init; } = true;
}
