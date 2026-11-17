using System;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Cli.Tests;

/// <summary>
/// Writes small synthetic dumps with the real dump writer: a manifest and, optionally, a section no loader knows, a
/// coarse terrain and water, a small network, or a few buildings, props and trees.
/// </summary>
internal static class TestDumps
{
    public static DumpManifest Manifest()
    {
        var manifest = new DumpManifest
        {
            CreatedUtc = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc),
            Exporter = new ExporterInfo("CsToBeamNG.Exporter", "0.1.0-test"),
            Game = new GameInfo { Version = "1.21.1-f9" },
            City = new CityInfo { Name = "Synthetic City", MapName = "Test Map", Environment = "Sunny" },
        };
        manifest.City.UnlockedTiles.Add(new TileCoordinate(4, 4));
        manifest.Counts["buildings"] = 12;
        manifest.Counts["trees"] = 183346;
        return manifest;
    }

    /// <summary>Writes a dump.</summary>
    /// <param name="folder">A new or empty folder.</param>
    /// <param name="manifest">The manifest, or <see langword="null"/> for <see cref="Manifest"/>.</param>
    /// <param name="complete">Whether the export finished.</param>
    /// <param name="unknownSection">The name of a section with one small JSON file, or <see langword="null"/> for none.</param>
    /// <returns>The dump folder's full path.</returns>
    /// <param name="terrainAndWater">Whether to add a coarse terrain and water (<see cref="AddTerrainAndWater"/>).</param>
    /// <param name="networks">Whether to add a small network (<see cref="AddNetworks"/>).</param>
    /// <param name="objects">Whether to add a few buildings, props and trees (<see cref="AddObjects"/>).</param>
    public static string Write(string folder, DumpManifest? manifest = null, bool complete = true, string? unknownSection = null, bool terrainAndWater = false, bool networks = false, bool objects = false)
    {
        manifest ??= Manifest();
        manifest.Complete = false;
        DumpWriter writer = DumpWriter.Create(folder, manifest);
        writer.WriteManifest();
        if (terrainAndWater)
        {
            AddTerrainAndWater(writer);
        }

        if (networks)
        {
            AddNetworks(writer);
        }

        if (objects)
        {
            AddObjects(writer);
        }

        if (unknownSection is not null)
        {
            SectionWriter section = writer.BeginSection(unknownSection, new DumpVersion(0, 3));
            using (JsonWriter json = section.CreateJsonFile("data.json"))
            {
                json.WriteStartObject();
                json.WriteEndObject();
            }

            section.RecordCount = 1;
            section.Complete();
        }

        manifest.Complete = complete;
        writer.WriteManifest();
        return writer.Folder;
    }

    /// <summary>
    /// Adds a basic road from x = −100 to 100 along z = 0 with one lane, and a one-way highway from its east end to an
    /// outside connection at x = −5000, beyond the unlocked tile (4, 4).
    /// </summary>
    private static void AddNetworks(DumpWriter writer)
    {
        var prefabs = new NetPrefabsSectionContent();
        prefabs.Prefabs.Add(Road(1, "Basic Road", "Level2", 8, highway: false));
        prefabs.Prefabs.Add(Road(2, "Highway", "Level5", 11, highway: true));
        NetPrefabsSectionWriter.Write(writer, prefabs);

        var nodes = new NetNodesSectionContent();
        nodes.Nodes.Add(new NetNodeRecord { Id = 1, Flags = NetNodeRecord.CreatedFlag | NetNodeRecord.EndFlag, Prefab = 1, Position = new DumpVector3(-100, 60, 0) });
        nodes.Nodes.Add(new NetNodeRecord { Id = 2, Flags = NetNodeRecord.CreatedFlag | NetNodeRecord.JunctionFlag | NetNodeRecord.TrafficLightsFlag, Prefab = 2, Position = new DumpVector3(100, 60, 0) });
        nodes.Nodes.Add(new NetNodeRecord { Id = 3, Flags = NetNodeRecord.CreatedFlag | NetNodeRecord.EndFlag | NetNodeRecord.OutsideFlag, Prefab = 2, Position = new DumpVector3(-5000, 60, 0) });
        NetNodesSectionWriter.Write(writer, nodes);

        var segments = new NetSegmentsSectionContent();
        segments.Segments.Add(Straight(1, 1, 1, 2, -100, 100, 8));
        segments.Segments.Add(Straight(2, 2, 2, 3, 100, -5000, 11));
        NetSegmentsSectionWriter.Write(writer, segments);

        var lanes = new NetLanesSectionContent();
        lanes.Lanes.Add(new NetLaneRecord { Id = 7, Segment = 1, Index = 0, Bezier = segments.Segments[0].Bezier, Length = 200 });
        NetLanesSectionWriter.Write(writer, lanes);
    }

    /// <summary>
    /// Adds a residential prefab with two houses inside the unlocked tile (4, 4), at (−100, 50) and (100, 150), and one
    /// far outside it at (−5000, 0); a bench prefab with one bench; and a tree prefab with two trees, one not grown.
    /// </summary>
    private static void AddObjects(DumpWriter writer)
    {
        var buildingPrefabs = new BuildingPrefabsSectionContent();
        var house = new BuildingPrefabRecord
        {
            Index = 3,
            Name = "L1 2x2 Detached",
            Class = new DumpItemClass { Name = "Low Residential - Level1", Service = "Residential", SubService = "ResidentialLow", Level = "Level1", Layer = 1 },
            AiType = "ResidentialBuildingAI",
            CellWidth = 2,
            CellLength = 2,
            Size = new DumpVector3(16, 10, 16),
            PlacementMode = "Roadside",
            ZoningMode = "Straight",
        };
        buildingPrefabs.Prefabs.Add(house);
        BuildingPrefabsSectionWriter.Write(writer, buildingPrefabs);

        var buildings = new BuildingsSectionContent();
        foreach ((uint id, float x, float z) in new[] { (1u, -100f, 50f), (2u, 100f, 150f), (3u, -5000f, 0f) })
        {
            buildings.Buildings.Add(new BuildingRecord
            {
                Id = id,
                Flags = BuildingRecord.CreatedFlag | BuildingRecord.CompletedFlag,
                Prefab = 3,
                Position = new DumpVector3(x, 60, z),
                MeshPosition = new DumpVector3(x, 60, z),
                Color = new DumpColor(1, 1, 1, 1),
                Width = 2,
                Length = 2,
            });
        }

        BuildingsSectionWriter.Write(writer, buildings);

        var propPrefabs = new PropPrefabsSectionContent();
        propPrefabs.Prefabs.Add(new PropPrefabRecord
        {
            Index = 4,
            Name = "Bench",
            Mesh = new DumpBounds(new DumpVector3(-1, 0, -0.5f), new DumpVector3(1, 1, 0.5f)),
            MinScale = 1,
            MaxScale = 1,
        });
        PropPrefabsSectionWriter.Write(writer, propPrefabs);
        var props = new PropsSectionContent();
        props.Props.Add(new PropRecord { Id = 1, Prefab = 4, Position = new DumpVector3(0, 60, 100), Scale = 1, Flags = PropRecord.CreatedFlag });
        PropsSectionWriter.Write(writer, props);

        var treePrefabs = new TreePrefabsSectionContent();
        treePrefabs.Prefabs.Add(new TreePrefabRecord { Index = 5, Name = "Oak", MinScale = 0.5f, MaxScale = 1.5f, MinBrightness = 0.75f, MaxBrightness = 1.25f });
        TreePrefabsSectionWriter.Write(writer, treePrefabs);
        var trees = new TreesSectionContent();
        trees.Trees.Add(new TreeRecord { Id = 1, Prefab = 5, Position = new DumpVector3(50, 60, 100), Scale = 0.5f, Brightness = 1, Flags = TreeRecord.CreatedFlag | 0x100 });
        trees.Trees.Add(new TreeRecord { Id = 2, Prefab = 5, Position = new DumpVector3(-50, 60, 100), Scale = 1.5f, Brightness = 1, Flags = TreeRecord.CreatedFlag });
        TreesSectionWriter.Write(writer, trees);
    }

    private static NetPrefabRecord Road(uint index, string name, string level, float halfWidth, bool highway)
    {
        var prefab = new NetPrefabRecord
        {
            Index = index,
            Name = name,
            ClassName = name,
            Service = "Road",
            SubService = "None",
            Level = level,
            Layer = NetPrefabRecord.DefaultLayer,
            AiType = "RoadAI",
            Highway = highway,
            HalfWidth = halfWidth,
            SegmentLength = 64,
        };
        prefab.Lanes.Add(new NetPrefabLane { Position = -2, Width = 3, SpeedLimit = 1, Direction = highway ? NetPrefabLane.ForwardDirection : NetPrefabLane.BackwardDirection, FinalDirection = highway ? NetPrefabLane.ForwardDirection : NetPrefabLane.BackwardDirection, LaneType = NetPrefabLane.VehicleLane, VehicleType = NetPrefabLane.CarVehicles });
        prefab.Lanes.Add(new NetPrefabLane { Position = 2, Width = 3, SpeedLimit = 1, Direction = NetPrefabLane.ForwardDirection, FinalDirection = NetPrefabLane.ForwardDirection, LaneType = NetPrefabLane.VehicleLane, VehicleType = NetPrefabLane.CarVehicles });
        return prefab;
    }

    // A straight segment along z = 0 with its corners half the width to each side; left of the direction is +Z when
    // going east.
    private static NetSegmentRecord Straight(uint id, uint prefab, uint startNode, uint endNode, float startX, float endX, float halfWidth)
    {
        float sign = Math.Sign(endX - startX);
        float third = (endX - startX) / 3;
        return new NetSegmentRecord
        {
            Id = id,
            Flags = 0x1,
            Prefab = prefab,
            StartNode = startNode,
            EndNode = endNode,
            StartDirection = new DumpVector3(sign, 0, 0),
            EndDirection = new DumpVector3(-sign, 0, 0),
            MiddlePosition = new DumpVector3((startX + endX) / 2, 60, 0),
            Bezier = new DumpBezier3(new DumpVector3(startX, 60, 0), new DumpVector3(startX + third, 60, 0), new DumpVector3(startX + (2 * third), 60, 0), new DumpVector3(endX, 60, 0)),
            StartLeft = new NetCorner(new DumpVector3(startX, 60, sign * halfWidth), new DumpVector3(sign, 0, 0), false),
            StartRight = new NetCorner(new DumpVector3(startX, 60, -sign * halfWidth), new DumpVector3(sign, 0, 0), false),
            EndLeft = new NetCorner(new DumpVector3(endX, 60, -sign * halfWidth), new DumpVector3(-sign, 0, 0), false),
            EndRight = new NetCorner(new DumpVector3(endX, 60, sign * halfWidth), new DumpVector3(-sign, 0, 0), false),
            AverageLength = Math.Abs(endX - startX),
        };
    }

    /// <summary>
    /// Adds a terrain of 10 × 10 heights 1920 m apart across the map, rising 1 m per 64 m towards the east, with detail
    /// heights 2 m higher in tile (4, 4); and water 3 m deep over the western half of the map, with one natural source.
    /// </summary>
    private static void AddTerrainAndWater(DumpWriter writer)
    {
        var grid = new GridGeometry(-8640, -8640, 1920, 10);
        ushort[] heights = new ushort[grid.PointCount];
        ushort[] depth = new ushort[grid.PointCount];
        for (int j = 0; j < grid.Resolution; j++)
        {
            for (int i = 0; i < grid.Resolution; i++)
            {
                heights[(j * grid.Resolution) + i] = (ushort)(6400 + (i * 1920));
                depth[(j * grid.Resolution) + i] = (ushort)(i < 5 ? 192 : 0);
            }
        }

        var terrain = new TerrainSectionContent(grid, heights, heights, new GridGeometry(-8640, -8640, 960, 19), 3);
        ushort[] detail = new ushort[9];
        for (int k = 0; k < detail.Length; k++)
        {
            detail[k] = (ushort)(6400 + 128 + ((4 * 1920) + ((k % 3) * 960)));
        }

        terrain.DetailTiles.Add(new TerrainDetailTile(new TileCoordinate(4, 4), detail));
        TerrainSectionWriter.Write(writer, terrain);

        int points = grid.PointCount;
        var water = new WaterSectionContent(40, grid, heights, depth, new ushort[points], new short[points], new short[points]);
        water.Sources.Add(new WaterSourceRecord { Id = 1, Type = WaterSourceRecord.NaturalType, Target = 6400, InputX = -5000, InputZ = 100, OutputX = -5000, OutputZ = 100 });
        WaterSectionWriter.Write(writer, water);
    }
}
