using System;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Level;

/// <summary>A level's <c>info.json</c>, which the game reads to list the level and to pick its default spawn point.</summary>
public static class LevelInfoFile
{
    /// <summary>The file name, in the level folder.</summary>
    public const string FileName = "info.json";

    /// <summary>Creates the file.</summary>
    /// <param name="levelName">The level's folder name.</param>
    /// <param name="metadata">What the file says.</param>
    /// <returns>The file, <c>/levels/&lt;level&gt;/info.json</c>.</returns>
    public static OutputFile Create(string levelName, LevelMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        VirtualPath path = VirtualPath.ForLevel(levelName).Combine(FileName);

        var spawnPoints = new JsonArray();
        foreach (LevelSpawnPoint spawnPoint in metadata.SpawnPoints)
        {
            BeamNGNames.ThrowIfNotSceneName(spawnPoint.ObjectName, nameof(metadata));
            spawnPoints.Add(new JsonObject
            {
                ["objectname"] = spawnPoint.ObjectName,
                ["translationId"] = spawnPoint.Title,
            });
        }

        // Keys in ordinal order.
        var document = new JsonObject();
        if (metadata.Authors is not null)
        {
            document.Add("authors", metadata.Authors);
        }

        if (metadata.SpawnPoints.Count > 0)
        {
            document.Add("defaultSpawnPointName", metadata.SpawnPoints[0].ObjectName);
        }

        if (metadata.Description is not null)
        {
            document.Add("description", metadata.Description);
        }

        document.Add("size", new JsonArray(metadata.Size.X, metadata.Size.Y));
        if (spawnPoints.Count > 0)
        {
            document.Add("spawnPoints", spawnPoints);
        }

        document.Add("supportsTimeOfDay", metadata.SupportsTimeOfDay);
        document.Add("supportsTraffic", metadata.SupportsTraffic);
        document.Add("title", metadata.Title);
        return new OutputFile(path, stream => BeamNGJson.WriteDocument(stream, document));
    }
}
