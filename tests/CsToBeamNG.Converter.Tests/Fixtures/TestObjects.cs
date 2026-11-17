using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Tests.Fixtures;

/// <summary>
/// Builds small synthetic sets of buildings, props and trees with their prefabs, laid out as the game stores them. Only
/// made-up data.
/// </summary>
internal sealed class TestObjects
{
    public const float Height = 60;

    /// <summary>The flags of a finished building.</summary>
    public const uint FinishedBuilding = BuildingRecord.CreatedFlag | BuildingRecord.CompletedFlag;

    /// <summary>The flags of a tree the game draws: created, grow state 1.</summary>
    public const ushort GrownTree = TreeRecord.CreatedFlag | 0x100;

    public BuildingPrefabsSectionContent BuildingPrefabs { get; } = new();

    public BuildingsSectionContent Buildings { get; } = new();

    public PropPrefabsSectionContent PropPrefabs { get; } = new();

    public PropsSectionContent Props { get; } = new();

    public TreePrefabsSectionContent TreePrefabs { get; } = new();

    public TreesSectionContent Trees { get; } = new();

    /// <summary>A building prefab of a service, with a mesh that fills its cells and four grey colour variations.</summary>
    public static BuildingPrefabRecord BuildingPrefab(uint index, string name, string service, int cellWidth = 2, int cellLength = 2)
    {
        float halfWidth = cellWidth * 4f;
        float halfLength = cellLength * 4f;
        var prefab = new BuildingPrefabRecord
        {
            Index = index,
            Name = name,
            Class = new DumpItemClass { Name = name, Service = service, SubService = "None", Level = "Level1", Layer = 1 },
            AiType = service + "BuildingAI",
            CellWidth = cellWidth,
            CellLength = cellLength,
            Size = new DumpVector3(2 * halfWidth, 10, 2 * halfLength),
            Mesh = new DumpBounds(new DumpVector3(-halfWidth, 0, -halfLength), new DumpVector3(halfWidth, 10, halfLength)),
            PlacementMode = "Roadside",
            ZoningMode = "Straight",
            FlattenTerrain = true,
        };
        for (int i = 0; i < prefab.Colors.Length; i++)
        {
            prefab.Colors[i] = new DumpColor(0.5f, 0.5f, 0.5f, 1);
        }

        return prefab;
    }

    /// <summary>A prop prefab whose mesh covers a box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static PropPrefabRecord PropPrefab(uint index, string name, DumpVector3 min, DumpVector3 max, bool decal = false, bool marker = false)
    {
        var prefab = new PropPrefabRecord
        {
            Index = index,
            Name = name,
            Class = new DumpItemClass { Name = "Beautification Item", Service = "Beautification", SubService = "None", Level = "Level1", Layer = 1 },
            Mesh = new DumpBounds(min, max),
            MinScale = 1,
            MaxScale = 1,
            Decal = decal,
            Marker = marker,
        };
        for (int i = 0; i < prefab.Colors.Length; i++)
        {
            prefab.Colors[i] = new DumpColor(1, 1, 1, 1);
        }

        return prefab;
    }

    /// <summary>A tree prefab whose mesh's crown reaches <paramref name="crownRadius"/> metres to each side.</summary>
    public static TreePrefabRecord TreePrefab(uint index, string name, float crownRadius) => new()
    {
        Index = index,
        Name = name,
        Class = new DumpItemClass { Name = "Beautification Item", Service = "Beautification", SubService = "None", Level = "Level1", Layer = 1 },
        Mesh = new DumpBounds(new DumpVector3(-crownRadius, 0, -crownRadius), new DumpVector3(crownRadius, 10, crownRadius)),
        MinScale = 0.5f,
        MaxScale = 1.5f,
        MinBrightness = 0.75f,
        MaxBrightness = 1.25f,
        DefaultColor = new DumpColor(0.25f, 0.5f, 0.25f, 1),
    };

    public TestObjects Add(BuildingPrefabRecord prefab)
    {
        BuildingPrefabs.Prefabs.Add(prefab);
        return this;
    }

    public TestObjects Add(PropPrefabRecord prefab)
    {
        PropPrefabs.Prefabs.Add(prefab);
        return this;
    }

    public TestObjects Add(TreePrefabRecord prefab)
    {
        TreePrefabs.Prefabs.Add(prefab);
        return this;
    }

    /// <summary>Adds a building whose mesh stands at its lot's centre.</summary>
    public BuildingRecord AddBuilding(uint id, uint prefab, float x, float z, float angle = 0, byte width = 2, byte length = 2, uint flags = FinishedBuilding, uint parent = 0)
    {
        var building = new BuildingRecord
        {
            Id = id,
            Flags = flags,
            Prefab = prefab,
            Position = new DumpVector3(x, Height, z),
            Angle = angle,
            MeshPosition = new DumpVector3(x, Height, z),
            ParentBuilding = parent,
            Color = new DumpColor(0.5f, 0.5f, 0.5f, 1),
            Width = width,
            Length = length,
            Level = 1,
        };
        Buildings.Buildings.Add(building);
        return building;
    }

    public PropRecord AddProp(uint id, uint prefab, float x, float z, float angle = 0, float scale = 1, ushort flags = PropRecord.CreatedFlag)
    {
        var prop = new PropRecord
        {
            Id = id,
            Prefab = prefab,
            Position = new DumpVector3(x, Height, z),
            Angle = angle,
            Scale = scale,
            Color = new DumpColor(1, 1, 1, 1),
            Flags = flags,
        };
        Props.Props.Add(prop);
        return prop;
    }

    public TreeRecord AddTree(uint id, uint prefab, float x, float z, float scale = 1, float brightness = 1, ushort flags = GrownTree)
    {
        var tree = new TreeRecord
        {
            Id = id,
            Prefab = prefab,
            Position = new DumpVector3(x, Height, z),
            Scale = scale,
            Brightness = brightness,
            Flags = flags,
        };
        Trees.Trees.Add(tree);
        return tree;
    }

    public SyntheticDump AddTo(SyntheticDump dump) => dump.WithObjects(BuildingPrefabs, Buildings, PropPrefabs, Props, TreePrefabs, Trees);
}
