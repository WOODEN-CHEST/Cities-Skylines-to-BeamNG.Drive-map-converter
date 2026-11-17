using System;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Dump.Tests;

/// <summary>Synthetic manifests, and the JSON that the writer must produce for them.</summary>
internal static class TestManifests
{
    public const string FullJson = """
        {
          "formatVersion": "0.1",
          "complete": true,
          "createdUtc": "2026-11-17T09:30:00Z",
          "exporter": {
            "name": "CsToBeamNG.Exporter",
            "version": "0.1.0"
          },
          "game": {
            "version": "1.17.1-f4",
            "mods": [
              {
                "name": "81 Tiles 2",
                "workshopId": "2862121823"
              },
              {
                "name": "Local Mod"
              }
            ]
          },
          "city": {
            "name": "Seaside",
            "mapName": "Green Plains",
            "environment": "Sunny",
            "mapTheme": "Alpine",
            "leftHandTraffic": true,
            "unlockedTiles": [
              {
                "x": 4,
                "z": 4
              },
              {
                "x": 4,
                "z": 5
              }
            ]
          },
          "counts": {
            "buildings": 567,
            "netNodes": 1234
          },
          "failureCounts": {
            "props": 2
          },
          "warnings": [
            "2 props were skipped: their prefab is missing."
          ],
          "sections": [
            {
              "name": "net-nodes",
              "version": "0.1",
              "recordCount": 1234,
              "files": [
                {
                  "path": "nodes.bin",
                  "bytes": 59296
                },
                {
                  "path": "strings.json",
                  "bytes": 120
                }
              ]
            },
            {
              "name": "terrain",
              "version": "1.2",
              "files": [
                {
                  "path": "heights/raw.bin",
                  "bytes": 2337226
                }
              ]
            }
          ]
        }

        """;

    public const string MinimalJson = """
        {
          "formatVersion": "0.1",
          "complete": true,
          "createdUtc": "2026-11-17T09:30:00Z",
          "exporter": {
            "name": "CsToBeamNG.Exporter",
            "version": "0.1.0"
          },
          "game": {
            "version": "1.17.1-f4",
            "mods": []
          },
          "city": {
            "name": "Seaside",
            "leftHandTraffic": false,
            "unlockedTiles": []
          },
          "counts": {},
          "failureCounts": {},
          "warnings": [],
          "sections": []
        }

        """;

    public static DateTime CreatedUtc { get; } = new(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>Only the required values.</summary>
    public static DumpManifest Minimal() => new()
    {
        Complete = true,
        CreatedUtc = CreatedUtc,
        Exporter = new ExporterInfo("CsToBeamNG.Exporter", "0.1.0"),
        Game = new GameInfo { Version = "1.17.1-f4" },
        City = new CityInfo { Name = "Seaside" },
    };

    /// <summary>Every field, matching <see cref="FullJson"/>.</summary>
    public static DumpManifest Full()
    {
        DumpManifest manifest = Minimal();
        manifest.Game.Mods.Add(new GameModInfo("81 Tiles 2", "2862121823"));
        manifest.Game.Mods.Add(new GameModInfo("Local Mod", null));
        manifest.City.MapName = "Green Plains";
        manifest.City.Environment = "Sunny";
        manifest.City.MapTheme = "Alpine";
        manifest.City.LeftHandTraffic = true;
        manifest.City.UnlockedTiles.Add(new TileCoordinate(4, 4));
        manifest.City.UnlockedTiles.Add(new TileCoordinate(4, 5));
        manifest.Counts["netNodes"] = 1234;
        manifest.Counts["buildings"] = 567;
        manifest.FailureCounts["props"] = 2;
        manifest.Warnings.Add("2 props were skipped: their prefab is missing.");
        manifest.Sections.Add(new SectionInfo
        {
            Name = "net-nodes",
            Version = new DumpVersion(0, 1),
            RecordCount = 1234,
            Files = { new SectionFileInfo("nodes.bin", 59296), new SectionFileInfo("strings.json", 120) },
        });
        manifest.Sections.Add(new SectionInfo
        {
            Name = "terrain",
            Version = new DumpVersion(1, 2),
            Files = { new SectionFileInfo("heights/raw.bin", 2337226) },
        });
        return manifest;
    }
}
