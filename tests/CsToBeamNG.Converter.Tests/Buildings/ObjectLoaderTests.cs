using System;
using System.Linq;
using CsToBeamNG.Converter.Buildings;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Props;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.Trees;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Buildings;

public sealed class ObjectLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LoadsTheSectionsOfBuildingsPropsAndTrees()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = SmallCity().AddTo(new SyntheticDump().WithUnlockedTiles(new TileCoordinate(4, 4))).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.All(result.Sections, section => Assert.Equal(SectionLoadStatus.Loaded, section.Status));
        Assert.Equal(
            ["building-prefabs", "buildings", "prop-prefabs", "props", "tree-prefabs", "trees"],
            result.Sections.Select(section => section.Section.Name));
        Assert.Empty(result.Messages);
        Assert.Equal(["terrain", "water", "net-prefabs", "net-nodes", "net-segments", "net-lanes"], result.MissingSections);
        CityBuildingPrefabs buildingPrefabs = result.World.Find<CityBuildingPrefabs>()!;
        CityBuildings buildings = result.World.Find<CityBuildings>()!;
        Assert.Equal(
            [BuildingCategory.Residential, BuildingCategory.Commercial, BuildingCategory.Other, BuildingCategory.Other],
            buildingPrefabs.Prefabs.Select(prefab => buildingPrefabs.CategoryOf(prefab.Index)));
        Assert.Equal(BuildingCategory.Other, buildingPrefabs.CategoryOf(99));
        Assert.True(buildings.TryGet(4, out BuildingRecord? sub));
        Assert.Equal(3u, sub.ParentBuilding);
        Assert.True(result.World.Find<CityPropPrefabs>()!.TryGet(21, out PropPrefabRecord? decal));
        Assert.True(decal.Decal);
        Assert.True(result.World.Find<CityProps>()!.TryGet(5, out PropRecord? fixedProp));
        Assert.True(fixedProp.HasFlags(PropRecord.FixedHeightFlag));
        Assert.True(result.World.Find<CityTreePrefabs>()!.TryGet(30, out _));
        Assert.True(result.World.Find<CityTrees>()!.TryGet(3, out TreeRecord? seedling));
        Assert.False(seedling.IsDrawn);

        // Neither the map editor's decoration nor the house beyond the unlocked tile counts; props and trees never do.
        Assert.Equal(new MapRect(-100, 0, 300, 300), buildings.AreaOfInterestBounds);
        Assert.Equal(6, buildings.ContentBuildings);
        Assert.All(result.World.Sections.Where(data => data is not CityBuildings), data => Assert.Null(data.AreaOfInterestBounds));
        AreaOfInterest area = AreaOfInterest.Plan(result.World);
        Assert.Equal(AreaOfInterestSource.Content, area.Source);
        Assert.Equal(new MapRect(-600, -500, 800, 800), area.Bounds);
    }

    [Fact]
    public void ReportsStatistics()
    {
        using var scope = new CultureScope("lv-LV");
        string folder = SmallCity().AddTo(new SyntheticDump().WithUnlockedTiles(new TileCoordinate(4, 4))).WriteTo(_temp.PathOf("dump"));

        CityWorld world = WorldLoader.Load(folder).World;

        Assert.Equal(
            [("prefabs", "4, 1 with sub-buildings"), ("by category", "1 residential, 1 commercial, 0 industrial, 0 office, 2 other")],
            Statistics(world.Find<CityBuildingPrefabs>()!));
        Assert.Equal(
            [
                ("buildings", "8: 1 sub-buildings, 1 not drawn"),
                ("by category", "3 residential, 2 commercial, 0 industrial, 0 office, 3 other"),
                ("states", "3 without the completed flag, 1 abandoned, 0 collapsed"),
                ("content", "6 buildings within x -100 to 300, z 0 to 300"),
            ],
            Statistics(world.Find<CityBuildings>()!));
        Assert.Equal([("prefabs", "3: 1 decals, 1 markers, 0 with lights")], Statistics(world.Find<CityPropPrefabs>()!));
        Assert.Equal(
            [("props", "5: 1 not drawn, 1 with a fixed height"), ("decals", "1"), ("scales", "0.5 to 2")],
            Statistics(world.Find<CityProps>()!));
        Assert.Equal([("prefabs", "1")], Statistics(world.Find<CityTreePrefabs>()!));
        Assert.Equal(
            [("trees", "4: 1 not drawn, 1 with a fixed height"), ("scales", "0.75 to 1.25"), ("brightness", "0.9 to 1.1")],
            Statistics(world.Find<CityTrees>()!));
    }

    [Fact]
    public void DescribesObjectsWithoutPrefabsAndEmptySections()
    {
        TestObjects objects = SmallCity();
        string folder = new SyntheticDump()
            .WithObjects(null, objects.Buildings, null, objects.Props, null, new TreesSectionContent())
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Empty(result.Messages);
        Assert.Equal("unknown without the building-prefabs section", Statistic(result.World.Find<CityBuildings>()!, "by category"));
        Assert.Equal("unknown without the prop-prefabs section", Statistic(result.World.Find<CityProps>()!, "decals"));
        Assert.Equal([("trees", "0: 0 not drawn, 0 with a fixed height"), ("scales", "none"), ("brightness", "none")], Statistics(result.World.Find<CityTrees>()!));

        // Without unlocked tiles, every building but the map editor's bounds the area of interest.
        Assert.Equal(new MapRect(-5000, 0, 300, 300), result.World.Find<CityBuildings>()!.AreaOfInterestBounds);
    }

    [Fact]
    public void ReportsReferencesToMissingObjectsAndKeepsTheObjects()
    {
        TestObjects objects = SmallCity();
        objects.AddBuilding(18, 99, 10, 10);
        objects.AddBuilding(9, 13, 20, 10, flags: TestObjects.FinishedBuilding | BuildingRecord.UntouchableFlag, parent: 77);
        objects.AddProp(9, 98, 10, 10);
        objects.AddTree(9, 97, 10, 10);
        string folder = objects.AddTo(new SyntheticDump()).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Equal(
            [
                "1 buildings have a prefab that the building-prefabs section lacks, such as building 18 (prefab 99); they are kept.",
                "1 sub-buildings belong to a building that the buildings section lacks, such as building 9 (parent 77); they are kept.",
                "1 props have a prefab that the prop-prefabs section lacks, such as prop 9 (prefab 98); they are kept.",
                "1 trees have a prefab that the tree-prefabs section lacks, such as tree 9 (prefab 97); they are kept.",
            ],
            result.Messages.Select(message => message.Message));
        Assert.All(result.Messages, message => Assert.Equal(LoadMessageSeverity.Warning, message.Severity));
        Assert.True(result.World.Find<CityBuildings>()!.TryGet(9, out _));
        Assert.True(result.World.Find<CityProps>()!.TryGet(9, out _));
        Assert.True(result.World.Find<CityTrees>()!.TryGet(9, out _));
    }

    [Fact]
    public void SkipsObjectsWithValuesThatArentFiniteAndDuplicateIds()
    {
        TestObjects objects = SmallCity();
        objects.AddBuilding(10, 10, 0, 0, angle: float.NaN);
        objects.AddProp(10, 20, 0, 0, scale: float.PositiveInfinity);
        objects.Props.Props.Add(new PropRecord { Id = 11, Prefab = 20, Position = new DumpVector3(1, 2, 3), Color = new DumpColor(float.NaN, 0, 0, 0), Flags = PropRecord.CreatedFlag });
        string folder = new SyntheticDump()
            .WithObjects(objects.BuildingPrefabs, objects.Buildings, objects.PropPrefabs, objects.Props, objects.TreePrefabs, null)
            .WithRawTrees((1, 0f, 1f), (2, 100f, 1f), (2, 5f, 1f), (3, 7f, float.NaN))
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Equal(
            [
                "Building 10 has a position, angle or colour that isn't finite, so it is skipped.",
                "Prop 10 has a position, angle, scale or colour that isn't finite, so it is skipped.",
                "Prop 11 has a position, angle, scale or colour that isn't finite, so it is skipped.",
                "Tree 2 is listed twice, so its second record is skipped.",
                "Tree 3 has a position, scale or brightness that isn't finite, so it is skipped.",
            ],
            result.Messages.Select(message => message.Message));
        Assert.False(result.World.Find<CityBuildings>()!.TryGet(10, out _));
        Assert.Equal([1u, 2u], result.World.Find<CityTrees>()!.Trees.Select(tree => tree.Id));
        Assert.Equal(100f, result.World.Find<CityTrees>()!.Trees[1].Position.X);
    }

    [Fact]
    public void RejectsListsWithAnIdTwice()
    {
        BuildingRecord building = new() { Id = 1 };
        PropRecord prop = new() { Id = 2 };
        TreeRecord tree = new() { Id = 3 };
        BuildingPrefabRecord buildingPrefab = TestObjects.BuildingPrefab(4, "A", "Residential");

        Assert.Throws<ArgumentException>(() => new CityBuildings([building, building], [], null));
        Assert.Throws<ArgumentException>(() => new CityProps([prop, prop], null));
        Assert.Throws<ArgumentException>(() => new CityTrees([tree, tree]));
        Assert.Throws<ArgumentException>(() => new CityBuildingPrefabs([buildingPrefab, buildingPrefab]));
        Assert.Throws<ArgumentException>(() => new CityPropPrefabs([TestObjects.PropPrefab(5, "B", default, default), TestObjects.PropPrefab(5, "C", default, default)]));
        Assert.Throws<ArgumentException>(() => new CityTreePrefabs([TestObjects.TreePrefab(6, "D", 1), TestObjects.TreePrefab(6, "E", 1)]));
    }

    // Four building prefabs (residential, commercial, a police station with a sub-building, the sub-building's) and
    // eight buildings: six of the player's in tile (4, 4), a map-editor decoration in it and a house beyond it; three prop
    // prefabs (bench, decal, marker) and five props; one tree prefab and four trees.
    internal static TestObjects SmallCity()
    {
        BuildingPrefabRecord police = TestObjects.BuildingPrefab(12, "Police Station", "PoliceDepartment", 3, 3);
        police.SubBuildings.Add(new BuildingSubBuilding { Prefab = 13, Name = "Police Garage", Position = new DumpVector3(10, 0, 0) });
        var objects = new TestObjects()
            .Add(TestObjects.BuildingPrefab(10, "L1 2x2 Detached", "Residential"))
            .Add(TestObjects.BuildingPrefab(11, "Shop", "Commercial", 2, 3))
            .Add(police)
            .Add(TestObjects.BuildingPrefab(13, "Police Garage", "PoliceDepartment", 1, 1))
            .Add(TestObjects.PropPrefab(20, "Bench", new DumpVector3(-1, 0, -0.5f), new DumpVector3(1, 1, 0.5f)))
            .Add(TestObjects.PropPrefab(21, "Decal", new DumpVector3(-2, 0, -2), new DumpVector3(2, 0.1f, 2), decal: true))
            .Add(TestObjects.PropPrefab(22, "Marker", new DumpVector3(-1, 0, -1), new DumpVector3(1, 1, 1), marker: true))
            .Add(TestObjects.TreePrefab(30, "Oak", 3));
        objects.AddBuilding(1, 10, 100, 100);
        objects.AddBuilding(2, 11, 200, 150, angle: (float)(Math.PI / 2), length: 3);
        objects.AddBuilding(3, 12, -100, 300, width: 3, length: 3);
        objects.AddBuilding(4, 13, -90, 300, width: 1, length: 1, flags: BuildingRecord.CreatedFlag | BuildingRecord.UntouchableFlag, parent: 3);
        objects.AddBuilding(5, 10, 300, 0, flags: TestObjects.FinishedBuilding | BuildingRecord.AbandonedFlag);
        objects.AddBuilding(6, 11, 0, 0, flags: BuildingRecord.CreatedFlag | BuildingRecord.HiddenFlag);
        objects.AddBuilding(7, 12, -800, -800, flags: BuildingRecord.CreatedFlag | BuildingRecord.OriginalFlag);
        objects.AddBuilding(8, 10, -5000, 0);
        objects.AddProp(1, 20, 110, 90, scale: 0.5f);
        objects.AddProp(2, 21, 120, 90, scale: 2);
        objects.AddProp(3, 22, 130, 90);
        objects.AddProp(4, 20, 140, 90, flags: PropRecord.CreatedFlag | PropRecord.HiddenFlag);
        objects.AddProp(5, 20, 150, 90, flags: PropRecord.CreatedFlag | PropRecord.FixedHeightFlag);
        objects.AddTree(1, 30, 50, 50, scale: 0.75f, brightness: 1.1f);
        objects.AddTree(2, 30, -5000, 5000, scale: 1.25f, brightness: 0.9f);
        objects.AddTree(3, 30, 60, 60, flags: TreeRecord.CreatedFlag);
        objects.AddTree(4, 30, 70, 60, flags: TestObjects.GrownTree | TreeRecord.FixedHeightFlag);
        return objects;
    }

    private static (string Name, string Value)[] Statistics(ISectionData data) =>
        [.. data.Statistics.Select(statistic => (statistic.Name, statistic.Value))];

    private static string Statistic(ISectionData data, string name) =>
        data.Statistics.Single(statistic => statistic.Name == name).Value;
}

/// <summary>Writes object sections record by record, so tests can give them what the section writers reject.</summary>
internal static class RawObjectSections
{
    public static SyntheticDump WithRawTrees(this SyntheticDump dump, params (uint Id, float X, float Scale)[] trees) =>
        dump.WithCustomSection(writer =>
        {
            SectionWriter section = writer.BeginSection(TreesSection.Name, TreesSection.Version);
            using (RecordFileWriter records = section.CreateRecordFile(TreesSection.TreesFileName, TreesSection.TreeRecordType, TreesSection.TreeRecordSize))
            {
                foreach ((uint id, float x, float scale) in trees)
                {
                    records.BeginRecord();
                    records.WriteUInt32(id);
                    records.WriteUInt32(30);
                    records.WriteSingle(x);
                    records.WriteSingle(TestObjects.Height);
                    records.WriteSingle(0);
                    records.WriteSingle(scale);
                    records.WriteSingle(1);
                    records.WriteUInt16(TestObjects.GrownTree);
                    records.WriteZeros(2);
                    records.EndRecord();
                }

                records.Finish();
            }

            section.RecordCount = trees.Length;
            section.Complete();
        });
}
