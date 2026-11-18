using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using CsToBeamNG.Converter.BeamNG;
using CsToBeamNG.Converter.BeamNG.Level;
using CsToBeamNG.Converter.BeamNG.Scene;
using CsToBeamNG.Converter.BeamNG.Terrain;
using CsToBeamNG.Converter.Coordinates;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>
/// Converts a loaded city into a BeamNG.drive level: the converter's stages 3 to 5 and 9 (docs/architecture.md,
/// Converter stages). So far the level has the city's terrain, painted by simple rules, the sea and the lakes, the sky,
/// and a spawn point; roads, buildings and trees come with later steps.
/// </summary>
/// <remarks>
/// The output depends only on the world and the options: the same dump gives the same files on every platform and in
/// every culture. Nothing is written here; <c>ModInstaller</c> installs the returned files.
/// </remarks>
public static class CityLevelConverter
{
    /// <summary>The terrain file's name, in the level folder.</summary>
    public const string TerrainFileName = "terrain.ter";

    /// <summary>The name of the spawn point in the middle of the level.</summary>
    public const string SpawnPointName = "spawn_centre";

    /// <summary>The largest <c>baseTexSize</c> given to the terrain, as the largest official levels use.</summary>
    public const int MaxBaseTextureSize = 4096;

    /// <summary>How high above the ground the player's vehicle spawns, in metres.</summary>
    public const float SpawnHeightMeters = 0.5f;

    // The terrain's materials, one per SurfaceKind in the enum's order, so a kind's value is its material index.
    private static readonly SurfaceKind[] _surfaceKinds = Enum.GetValues<SurfaceKind>();

    /// <summary>Converts a city.</summary>
    /// <param name="loaded">The loaded dump; its messages go into the report.</param>
    /// <param name="options">What to make.</param>
    /// <param name="progress">Called with a short description at the start of each stage, or <see langword="null"/>.</param>
    /// <returns>The level's files and the report.</returns>
    /// <exception cref="ConversionException">The dump has no terrain.</exception>
    public static ConversionResult Convert(WorldLoadResult loaded, ConversionOptions options, Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(options);
        CityWorld world = loaded.World;
        CityTerrain terrain = world.Find<CityTerrain>() ?? throw new ConversionException(
            "The dump has no terrain the converter could load, so there is nothing to build a level on. `cstobeamng inspect` says why.");
        CityWater? water = world.Find<CityWater>();
        var notes = new List<string>();
        foreach (LoadMessage message in loaded.Messages)
        {
            if (message.Severity != LoadMessageSeverity.Info)
            {
                notes.Add((message.Severity == LoadMessageSeverity.Error ? "error: " : "warning: ") + message.Message);
            }
        }

        // Stage 3: the area and the terrain grid.
        progress?.Invoke("Planning the terrain");
        (MapRect area, string areaSource) = ChooseArea(world, options);
        GridGeometry coarseGrid = terrain.FinalHeights.Geometry;
        double mapEnd = (coarseGrid.Resolution - 1) * (double)coarseGrid.Spacing;
        var map = new MapRect(coarseGrid.OriginX, coarseGrid.OriginZ, (float)(coarseGrid.OriginX + mapEnd), (float)(coarseGrid.OriginZ + mapEnd));
        TerrainGrid grid = TerrainGrid.Plan(area, terrain.DetailGrid, map, options.TerrainGrid);
        if (!grid.CoversArea)
        {
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"warning: the area is larger than a terrain of {options.TerrainGrid.MaxSize} samples can cover; the level shows only {grid.Coverage}."));
        }

        // Stage 4: heights.
        progress?.Invoke(string.Create(CultureInfo.InvariantCulture, $"Sampling the terrain: {grid.Size} × {grid.Size} samples, {grid.SquareSize:0.###} m apart"));
        CoarseInterpolation coarse = options.CoarseInterpolation;
        double Ground(double x, double z) => terrain.SampleGround(x, z, coarse);
        TerrainHeightField field = TerrainHeightField.Sample(grid, Ground);
        double detailShare = DetailShare(grid, terrain);

        // Stage 5: water, which also edits the terrain under lakes that reach below the sea.
        progress?.Invoke("Finding the sea and the lakes");
        WaterBodies? bodies = water is null ? null : WaterBodyFinder.Find(water, Ground, grid.Coverage, options.Water);
        NearbyWater? nearbyWater = water is null ? null : new NearbyWater(water);
        if (water is null)
        {
            notes.Add("warning: the dump has no water, so the level has none.");
        }

        // Stage 4, continued: the edits, then the heights the terrain must store.
        TerrainEditLayer edits = options.TerrainEdits ?? new TerrainEditLayer();
        long editedSamples = field.Apply(edits);
        long raisedLakeBedSamples = bodies is null ? 0 : field.Apply(LakeBedEdits.Create(bodies));
        (float lowest, float highest) = field.GetRange();
        TerrainHeightRange heightRange = TerrainHeightRange.Plan(lowest, highest);

        progress?.Invoke("Painting the terrain");
        SurfaceKind[] kinds = options.SurfaceRules.Classify(field, nearbyWater);
        long[] surfaceCounts = new long[_surfaceKinds.Length];
        foreach (SurfaceKind kind in kinds)
        {
            surfaceCounts[(int)kind]++;
        }

        TerrainData terrainData = CreateTerrainData(field, heightRange, kinds);

        // Stage 9: the level.
        progress?.Invoke("Assembling the level");
        bool IsWet(int column, int row) => IsWetAt(grid, field, bodies, nearbyWater, column, row);
        (double middleColumn, double middleRow) = grid.ToSample((area.MinX + area.MaxX) / 2.0, (area.MinZ + area.MaxZ) / 2.0);
        int startColumn = Math.Clamp((int)Math.Round(middleColumn), 0, grid.Size - 1);
        int startRow = Math.Clamp((int)Math.Round(middleRow), 0, grid.Size - 1);
        (int Column, int Row)? spawnSample = SpawnPlacement.FindNear(field, startColumn, startRow, IsWet);
        (int spawnColumn, int spawnRow) = spawnSample ?? (startColumn, startRow);
        Vector3 spawnPosition = grid.ToBeamNG(spawnColumn, spawnRow, field[spawnColumn, spawnRow] + SpawnHeightMeters);

        string levelName = options.LevelName;
        VirtualPath levelFolder = VirtualPath.ForLevel(levelName);
        VirtualPath terFile = levelFolder.Combine(TerrainFileName);
        TerrainMaterialTextureSet textureSet = TerrainMaterialPresets.TextureSet(levelName + "_TerrainMaterialTextureSet");
        var terrainMaterials = new List<TerrainMaterial>();
        foreach (SurfaceKind kind in _surfaceKinds)
        {
            terrainMaterials.Add(MaterialFor(kind));
        }

        List<SceneObject> waterObjects = CreateWaterObjects(bodies, out List<int> lakeBlockCounts);
        SimGroup missionGroup = CreateScene(grid, heightRange, terFile, textureSet, waterObjects, spawnPosition);
        CityInfoText city = CityInfoText.From(world);
        var report = new ConversionReport
        {
            LevelName = levelName,
            DumpName = Path.GetFileName(Path.TrimEndingDirectorySeparator(world.DumpFolder)),
            CityName = city.Name,
            MapName = city.Map,
            GameVersion = world.Manifest.Game.Version,
            ExportedUtc = world.Manifest.CreatedUtc,
            Area = area,
            AreaSource = areaSource,
            Grid = grid,
            HeightRange = heightRange,
            LowestHeight = lowest,
            HighestHeight = highest,
            DetailShare = detailShare,
            CoarseInterpolation = coarse,
            EditCount = edits.Stamps.Count,
            EditedSamples = editedSamples,
            RaisedLakeBedSamples = raisedLakeBedSamples,
            SurfaceCounts = surfaceCounts,
            Water = bodies,
            LakeBlockCounts = lakeBlockCounts,
            SpawnName = SpawnPointName,
            SpawnPosition = spawnPosition,
            SpawnOnDryGround = spawnSample is not null,
            NotConverted = NotConverted(world),
            Notes = notes,
        };

        var metadata = new LevelMetadata
        {
            Title = city.Name,
            Description = city.Map.Length == 0
                ? $"The Cities: Skylines city {city.Name}, converted by CsToBeamNG."
                : $"The Cities: Skylines city {city.Name} on the map {city.Map}, converted by CsToBeamNG.",
            Authors = "CsToBeamNG",
            Size = ((int)Math.Round(grid.Size * (double)grid.SquareSize), (int)Math.Round(grid.Size * (double)grid.SquareSize)),
            SpawnPoints = [new LevelSpawnPoint(SpawnPointName, "City centre")],

            // No AI roads yet, so there is nothing for traffic to drive on (step 1.8 adds them).
            SupportsTraffic = false,
        };

        string reportText = report.ToText();
        var files = new List<OutputFile>
        {
            LevelInfoFile.Create(levelName, metadata),
            new(terFile, stream => TerFile.Write(stream, terrainData)),
            TerrainJsonFile.Create(levelFolder.Combine("terrain.terrain.json"), terFile, terrainData),
            TerrainMaterialsFile.Create(levelFolder.Combine(TerrainMaterialsFile.DefaultRelativePath), levelName, textureSet, terrainMaterials),
            new(levelFolder.Combine(ConversionReport.FileName), stream => stream.Write(Encoding.UTF8.GetBytes(reportText))),
        };
        files.AddRange(SceneTreeWriter.CreateFiles(levelName, missionGroup));
        return new ConversionResult(levelName, files, report);
    }

    /// <summary>The terrain material that paints a kind of surface.</summary>
    /// <param name="kind">The kind.</param>
    public static TerrainMaterial MaterialFor(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Grass => TerrainMaterialPresets.Grass("grass"),
        SurfaceKind.Dirt => TerrainMaterialPresets.Dirt("dirt"),
        SurfaceKind.Rock => TerrainMaterialPresets.Rock("rock"),
        SurfaceKind.Sand => TerrainMaterialPresets.Sand("sand"),
        SurfaceKind.Asphalt => TerrainMaterialPresets.Asphalt("asphalt"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown surface kind."),
    };

    private static (MapRect Area, string Source) ChooseArea(CityWorld world, ConversionOptions options)
    {
        if (options.Area is MapRect chosen)
        {
            return (chosen, options.AreaDescription ?? "chosen by the user");
        }

        AreaOfInterest areaOfInterest = AreaOfInterest.Plan(world);
        string source = areaOfInterest.Source switch
        {
            AreaOfInterestSource.Content => string.Create(
                CultureInfo.InvariantCulture,
                $"the area of interest: the sections' content plus a {AreaOfInterestOptions.DefaultMarginMeters:0.##} m margin, within the map"),
            AreaOfInterestSource.UnlockedTiles => "the area of interest: the unlocked tiles, as no section gives content bounds",
            AreaOfInterestSource.Tiles => "the area of interest: the chosen tiles",
            _ => "the area of interest: the whole map, as the dump has neither content bounds nor unlocked tiles",
        };
        return (areaOfInterest.Bounds, source);
    }

    // The share of samples that lie where the game has 4 m detail heights.
    private static double DetailShare(TerrainGrid grid, CityTerrain terrain)
    {
        long[] perRow = new long[grid.Size];
        Parallel.For(0, grid.Size, row =>
        {
            long count = 0;
            for (int column = 0; column < grid.Size; column++)
            {
                (double x, double z) = grid.ToCitiesSkylines(column, row);
                if (terrain.HasDetailAt(x, z))
                {
                    count++;
                }
            }

            perRow[row] = count;
        });
        long total = 0;
        foreach (long count in perRow)
        {
            total += count;
        }

        return total / ((double)grid.Size * grid.Size);
    }

    private static TerrainData CreateTerrainData(TerrainHeightField field, TerrainHeightRange heightRange, SurfaceKind[] kinds)
    {
        var names = new List<string>();
        foreach (SurfaceKind kind in _surfaceKinds)
        {
            names.Add(MaterialFor(kind).InternalName);
        }

        var data = new TerrainData(field.Size, names);
        Span<ushort> stored = data.Heights;
        Span<byte> materials = data.Materials;
        ReadOnlySpan<float> heights = field.Heights;
        for (int k = 0; k < heights.Length; k++)
        {
            stored[k] = heightRange.ToStored(heights[k]);
            materials[k] = (byte)kinds[k];
        }

        return data;
    }

    // Whether a sample lies under the level's water or right next to the city's water, with a metre to spare.
    private static bool IsWetAt(TerrainGrid grid, TerrainHeightField field, WaterBodies? bodies, NearbyWater? nearby, int column, int row)
    {
        const double Margin = 1;
        double height = field[column, row];
        if (bodies?.SeaSurface is double sea && height < sea + Margin)
        {
            return true;
        }

        (double x, double z) = grid.ToCitiesSkylines(column, row);
        if (nearby is not null && nearby.SurfaceNear(x, z) is double surface && !double.IsNaN(surface) && height < surface + Margin)
        {
            return true;
        }

        foreach (Lake lake in bodies?.Lakes ?? [])
        {
            if (height < lake.Surface + Margin)
            {
                foreach (MapRect rectangle in lake.Rectangles)
                {
                    if (x >= rectangle.MinX && x <= rectangle.MaxX && z >= rectangle.MinZ && z <= rectangle.MaxZ)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static List<SceneObject> CreateWaterObjects(WaterBodies? bodies, out List<int> lakeBlockCounts)
    {
        var objects = new List<SceneObject>();
        lakeBlockCounts = [];
        if (bodies is null)
        {
            return objects;
        }

        if (bodies.SeaSurface is double sea)
        {
            objects.Add(new WaterPlane { Key = "water/sea", Name = "sea", Height = (float)sea, Style = WaterStyles.Sea() });
        }

        for (int n = 0; n < bodies.Lakes.Count; n++)
        {
            Lake lake = bodies.Lakes[n];
            for (int m = 0; m < lake.Rectangles.Count; m++)
            {
                MapRect rectangle = lake.Rectangles[m];
                Vector3 first = CoordinateMapping.ToBeamNG(new Vector3(rectangle.MinX, (float)lake.Surface, rectangle.MinZ));
                Vector3 second = CoordinateMapping.ToBeamNG(new Vector3(rectangle.MaxX, (float)lake.Surface, rectangle.MaxZ));
                Vector3 min = Vector3.Min(first, second);
                Vector3 max = Vector3.Max(first, second);
                float width = max.X - min.X;
                float length = max.Y - min.Y;
                objects.Add(new WaterBlock
                {
                    Key = string.Create(CultureInfo.InvariantCulture, $"water/lake/{n + 1}/{m + 1}"),
                    Name = string.Create(CultureInfo.InvariantCulture, $"lake_{n + 1}_{m + 1}"),
                    SurfaceCentre = new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, (float)lake.Surface),
                    Width = width,
                    Length = length,
                    Depth = (float)Math.Max(1, lake.Surface - lake.Bottom),
                    GridElementSize = GridElementSize(width, length),
                    Style = WaterStyles.Lake(),
                });
            }

            lakeBlockCounts.Add(lake.Rectangles.Count);
        }

        return objects;
    }

    // The largest square size of at most 4 m that fits a whole number of times along both sides; the game draws
    // nothing of a remainder (docs/research/beamng-level-format.md, Water).
    private static float GridElementSize(float width, float length)
    {
        foreach (float size in (ReadOnlySpan<float>)[4, 2, 1])
        {
            if (IsMultiple(width, size) && IsMultiple(length, size))
            {
                return size;
            }
        }

        return 1;

        static bool IsMultiple(float value, float size) => Math.Abs((value / size) - Math.Round(value / size)) < 1e-3;
    }

    private static SimGroup CreateScene(
        TerrainGrid grid, TerrainHeightRange heightRange, VirtualPath terFile, TerrainMaterialTextureSet textureSet, List<SceneObject> waterObjects, Vector3 spawnPosition)
    {
        float visibleDistance = (float)Math.Clamp(2 * grid.ExtentMeters, 4000, 20000);
        var skyAndSun = new SimGroup("sky_and_sun");
        skyAndSun.Children.Add(LevelEnvironment.CreateLevelInfo("environment/level-info", visibleDistance));
        skyAndSun.Children.Add(LevelEnvironment.CreateTimeOfDay("environment/time-of-day", time: 0.1f));
        skyAndSun.Children.Add(LevelEnvironment.CreateSky("environment/sky"));

        var terrainGroup = new SimGroup("terrain");
        terrainGroup.Children.Add(new TerrainBlock
        {
            Key = "terrain",
            Name = "theTerrain",
            TerrainFile = terFile,
            Position = new Vector3(grid.BeamNGPosition, heightRange.ZOffset),
            SquareSize = grid.SquareSize,
            MaxHeight = heightRange.MaxHeight,
            MaterialTextureSet = textureSet.Name,
            BaseTexSize = Math.Min(grid.Size, MaxBaseTextureSize),
        });

        var levelObjects = new SimGroup("level_objects");
        levelObjects.Children.Add(skyAndSun);
        levelObjects.Children.Add(terrainGroup);
        if (waterObjects.Count > 0)
        {
            var waterGroup = new SimGroup("water");
            foreach (SceneObject waterObject in waterObjects)
            {
                waterGroup.Children.Add(waterObject);
            }

            levelObjects.Children.Add(waterGroup);
        }

        var spawnPoints = new SimGroup("PlayerDropPoints");
        spawnPoints.Children.Add(new SpawnSphere
        {
            Key = "spawn/centre",
            Name = SpawnPointName,
            Position = spawnPosition,
            Rotation = SpawnSphere.RotationForHeading(new Vector2(0, 1)),
        });

        var missionGroup = new SimGroup(SceneTreeWriter.RootGroupName);
        missionGroup.Children.Add(levelObjects);
        missionGroup.Children.Add(spawnPoints);
        return missionGroup;
    }

    // What the dump has that the level doesn't show yet, by the manifest's counts.
    private static List<string> NotConverted(CityWorld world)
    {
        var parts = new List<string>();
        foreach ((string key, string label) in (ReadOnlySpan<(string, string)>)[
            ("netSegments", "network segments (steps 1.6 to 1.8)"),
            ("buildings", "buildings (step 1.9)"),
            ("props", "props"),
            ("trees", "trees (step 1.9)")])
        {
            if (world.Manifest.Counts.TryGetValue(key, out long count) && count > 0)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{count} {label}"));
            }
        }

        return parts;
    }

    // The city's name and map as the report and info.json show them.
    private sealed record CityInfoText(string Name, string Map)
    {
        public static CityInfoText From(CityWorld world)
        {
            string name = string.IsNullOrWhiteSpace(world.Manifest.City.Name) ? "Unnamed city" : world.Manifest.City.Name;
            return new CityInfoText(name, world.Manifest.City.MapName ?? string.Empty);
        }
    }
}
