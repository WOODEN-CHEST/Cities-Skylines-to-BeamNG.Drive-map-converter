using System;
using System.Collections.Generic;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Level;
using CsToBeamNG.Converter.BeamNG.Scene;
using CsToBeamNG.Converter.BeamNG.Shapes;
using CsToBeamNG.Converter.BeamNG.Terrain;

namespace CsToBeamNG.Converter.Development;

/// <summary>
/// A small test level that shows the BeamNG writers produce a level the game loads: a 1 km terrain with one hill
/// and an asphalt strip, an invisible AI road along the strip, a box with collision, and a spawn point at the start
/// of the road. <c>cstobeamng dev hello-level</c> installs it.
/// </summary>
/// <remarks>
/// The layout is asymmetric, so a mirrored or transposed terrain shows: the hill lies at (+300, +150), the road
/// runs from west to east south of the centre. All coordinates are BeamNG world coordinates in metres. The geometry
/// uses no trigonometry, so the output is the same on every platform.
/// </remarks>
public static class HelloLevel
{
    /// <summary>The mod folder in <c>mods/unpacked</c>.</summary>
    public const string ModFolderName = "cstobeamng_hello";

    /// <summary>The level's folder name.</summary>
    public const string LevelName = "cstobeamng_hello";

    /// <summary>The terrain's samples per side.</summary>
    public const int TerrainSize = 1024;

    /// <summary>The distance between terrain samples, in metres.</summary>
    public const float SquareSize = 1;

    /// <summary>The terrain's <c>maxHeight</c>.</summary>
    public const float MaxHeight = 100;

    /// <summary>The height of the flat ground, in world z.</summary>
    public const float GroundHeight = 20;

    /// <summary>The radius of the hill, in metres.</summary>
    public const float HillRadius = 160;

    /// <summary>How far the hill's top rises above the flat ground, in metres.</summary>
    public const float HillHeight = 40;

    /// <summary>The road's width: two lanes of 3.5 m.</summary>
    public const float RoadWidth = 7;

    /// <summary>The distance from the road's centre line within which the terrain is asphalt.</summary>
    public const float AsphaltHalfWidth = 5;

    /// <summary>The name of the spawn point.</summary>
    public const string SpawnPointName = "spawn_default";

    /// <summary>The terrain's lower-left corner.</summary>
    public static Vector3 TerrainPosition { get; } = new(-512, -512, 0);

    /// <summary>The centre of the hill.</summary>
    public static Vector2 HillCentre { get; } = new(300, 150);

    /// <summary>The road's nodes, from west to east: a gentle S-curve on flat ground.</summary>
    public static IReadOnlyList<Vector2> RoadPoints { get; } =
    [
        new(-450, -300), new(-350, -300), new(-250, -292), new(-150, -268), new(-50, -240),
        new(50, -228), new(150, -236), new(250, -262), new(350, -290), new(450, -300),
    ];

    /// <summary>The box's size: 8 m along x, 6 m along y, 4 m high.</summary>
    public static Vector3 BoxSize { get; } = new(8, 6, 4);

    /// <summary>The centre of the box's base, on the flat ground north of the road.</summary>
    public static Vector3 BoxPosition { get; } = new(0, -190, GroundHeight);

    /// <summary>Where the player spawns: in the right-hand lane near the road's west end, 0.5 m above the ground.</summary>
    public static Vector3 SpawnPosition { get; } = new(-430, -301.75f, GroundHeight + 0.5f);

    /// <summary>The direction the player faces at spawn: east, along the road.</summary>
    public static Vector2 SpawnDirection { get; } = new(1, 0);

    private static VirtualPath LevelFolder => VirtualPath.ForLevel(LevelName);

    /// <summary>The intended height of the ground at a point, in world z.</summary>
    /// <param name="x">The world x.</param>
    /// <param name="y">The world y.</param>
    /// <returns>The flat ground's height plus the hill.</returns>
    public static double GroundHeightAt(double x, double y)
    {
        double dx = x - HillCentre.X;
        double dy = y - HillCentre.Y;
        double t = 1 - (((dx * dx) + (dy * dy)) / ((double)HillRadius * HillRadius));

        // A smooth bump, (1 - r²/R²)², which needs no transcendental functions.
        return GroundHeight + (t > 0 ? HillHeight * t * t : 0);
    }

    /// <summary>The shortest distance from a point to the road's centre line.</summary>
    /// <param name="x">The world x.</param>
    /// <param name="y">The world y.</param>
    /// <returns>The distance in metres.</returns>
    public static double DistanceToRoad(double x, double y)
    {
        double best = double.MaxValue;
        for (int i = 1; i < RoadPoints.Count; i++)
        {
            Vector2 a = RoadPoints[i - 1];
            Vector2 b = RoadPoints[i];
            double abX = b.X - a.X;
            double abY = b.Y - a.Y;
            double t = Math.Clamp((((x - a.X) * abX) + ((y - a.Y) * abY)) / ((abX * abX) + (abY * abY)), 0, 1);
            double dx = x - (a.X + (t * abX));
            double dy = y - (a.Y + (t * abY));
            best = Math.Min(best, (dx * dx) + (dy * dy));
        }

        return Math.Sqrt(best);
    }

    /// <summary>Creates the level's files.</summary>
    /// <returns>The files, at their virtual paths inside the mod folder.</returns>
    public static IReadOnlyList<OutputFile> CreateFiles()
    {
        VirtualPath terFile = LevelFolder.Combine("terrain.ter");
        TerrainData terrain = CreateTerrain();
        TerrainMaterialTextureSet textureSet = TerrainMaterialPresets.TextureSet("cstobeamng_hello_TerrainMaterialTextureSet");
        TerrainMaterial[] terrainMaterials = [TerrainMaterialPresets.Grass("grass"), TerrainMaterialPresets.Asphalt("asphalt")];

        string boxMaterial = "cstobeamng_hello_box";
        VirtualPath boxShapeFile = LevelFolder.Combine("art/shapes/hello_box.dae");
        var boxShape = new ColladaShape
        {
            Name = "hello_box",
            Details = [new ShapeDetail(ShapeMeshes.Box(BoxSize, boxMaterial), pixelSize: 100)],
            CollisionMesh = ShapeMeshes.Box(BoxSize, boxMaterial),
        };

        SimGroup missionGroup = CreateScene(terFile, textureSet, boxShapeFile);
        var metadata = new LevelMetadata
        {
            Title = "CsToBeamNG hello level",
            Description = "A test level generated by cstobeamng: terrain with a hill, an AI road and a box.",
            Authors = "CsToBeamNG",
            Size = (TerrainSize, TerrainSize),
            SpawnPoints = [new LevelSpawnPoint(SpawnPointName, "Road start")],
        };

        var files = new List<OutputFile>
        {
            LevelInfoFile.Create(LevelName, metadata),
            new(terFile, stream => TerFile.Write(stream, terrain)),
            TerrainJsonFile.Create(LevelFolder.Combine("terrain.terrain.json"), terFile, terrain),
            TerrainMaterialsFile.Create(LevelFolder.Combine(TerrainMaterialsFile.DefaultRelativePath), LevelName, textureSet, terrainMaterials),
            ColladaFile.Create(boxShapeFile, boxShape),
            MaterialsFile.Create(
                LevelFolder.Combine("art/shapes/main.materials.json"),
                LevelName,
                [new ShapeMaterial { Name = boxMaterial, BaseColorFactor = new Vector4(0.9f, 0.45f, 0.1f, 1), RoughnessFactor = 0.6f, MetallicFactor = 0 }]),
        };
        files.AddRange(SceneTreeWriter.CreateFiles(LevelName, missionGroup));
        return files;
    }

    private static TerrainData CreateTerrain()
    {
        var terrain = new TerrainData(TerrainSize, ["grass", "asphalt"]);
        var scale = new TerrainHeightScale(MaxHeight);
        const byte Asphalt = 1;
        for (int row = 0; row < TerrainSize; row++)
        {
            double y = TerrainPosition.Y + ((double)row * SquareSize);
            for (int column = 0; column < TerrainSize; column++)
            {
                double x = TerrainPosition.X + ((double)column * SquareSize);
                terrain.SetHeight(column, row, scale.ToStored(GroundHeightAt(x, y) - TerrainPosition.Z));
                if (DistanceToRoad(x, y) <= AsphaltHalfWidth)
                {
                    terrain.SetMaterial(column, row, Asphalt);
                }
            }
        }

        return terrain;
    }

    private static SimGroup CreateScene(VirtualPath terFile, TerrainMaterialTextureSet textureSet, VirtualPath boxShapeFile)
    {
        var skyAndSun = new SimGroup("sky_and_sun");
        skyAndSun.Children.Add(LevelEnvironment.CreateLevelInfo("environment/level-info", visibleDistance: 4000));
        skyAndSun.Children.Add(LevelEnvironment.CreateTimeOfDay("environment/time-of-day", time: 0.1f));
        skyAndSun.Children.Add(LevelEnvironment.CreateSky("environment/sky"));

        var terrainGroup = new SimGroup("terrain");
        terrainGroup.Children.Add(new TerrainBlock
        {
            Key = "terrain",
            Name = "theTerrain",
            TerrainFile = terFile,
            Position = TerrainPosition,
            SquareSize = SquareSize,
            MaxHeight = MaxHeight,
            MaterialTextureSet = textureSet.Name,
        });

        var levelObjects = new SimGroup("level_objects");
        levelObjects.Children.Add(skyAndSun);
        levelObjects.Children.Add(terrainGroup);

        var spawnPoints = new SimGroup("PlayerDropPoints");
        spawnPoints.Children.Add(new SpawnSphere
        {
            Key = "spawn/default",
            Name = SpawnPointName,
            Position = SpawnPosition,
            Rotation = SpawnSphere.RotationForHeading(SpawnDirection),
        });

        var roadNodes = new List<DecalRoadNode>();
        foreach (Vector2 point in RoadPoints)
        {
            roadNodes.Add(new DecalRoadNode(new Vector3(point, (float)GroundHeightAt(point.X, point.Y)), RoadWidth));
        }

        var aiRoads = new SimGroup("ai_roads");
        aiRoads.Children.Add(new DecalRoad
        {
            Key = "road/main",
            Nodes = roadNodes,
            Material = DecalRoad.InvisibleMaterial,
            Drivability = 1,
            LanesLeft = 1,
            LanesRight = 1,
            ImprovedSpline = true,
        });

        var objects = new SimGroup("objects");
        objects.Children.Add(new TSStatic
        {
            Key = "object/box",
            ShapeName = boxShapeFile,
            Position = BoxPosition,
        });

        var missionGroup = new SimGroup(SceneTreeWriter.RootGroupName);
        missionGroup.Children.Add(levelObjects);
        missionGroup.Children.Add(spawnPoints);
        missionGroup.Children.Add(aiRoads);
        missionGroup.Children.Add(objects);
        return missionGroup;
    }
}
