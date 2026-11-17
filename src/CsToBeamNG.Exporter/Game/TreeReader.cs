using System;
using System.Collections.Generic;
using ColossalFramework.Math;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the trees and their prefabs into the contents of the dump's <c>trees</c> and <c>tree-prefabs</c> sections,
/// without changing anything (docs/research/cs1-assets.md, Placement). Create it on the main thread; read on the
/// simulation thread, where the game changes the trees.
/// </summary>
/// <remarks>
/// Positions come through the game's accessor, which mods that store them more precisely replace. Scale and
/// brightness come from the game's randomizer seeded with the tree's id, in the order <c>TreeInstance.RenderInstance</c>
/// draws them. A tree that can't be read is skipped and counted in the <see cref="ObjectFailures"/> passed in.
/// </remarks>
internal sealed class TreeReader
{
    // The renderer's scale and brightness: the prefab's minimum plus Int32(10000) × the range × 0.0001.
    private const uint RandomSteps = 10000;
    private const float RandomStep = 0.0001f;

    private readonly TreeManager _trees;

    private TreeReader(TreeManager trees) => _trees = trees;

    /// <summary>Finds the tree manager. Main thread only (<see cref="CityReader.Require{T}"/>).</summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    public static TreeReader Create() => new(CityReader.Require<TreeManager>());

    /// <summary>Reads every created tree. Simulation thread only.</summary>
    /// <param name="failures">Where to count the trees that can't be read.</param>
    public TreesSectionContent ReadTrees(ObjectFailures failures)
    {
        var content = new TreesSectionContent();
        TreeInstance[] trees = _trees.m_trees.m_buffer;
        for (int i = 0; i < trees.Length; i++)
        {
            if (((TreeInstance.Flags)trees[i].m_flags & TreeInstance.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                content.Trees.Add(ReadTree((uint)i, trees[i]));
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<TreeInfo>("Tree", i, trees[i].m_infoIndex) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>Reads the prefabs that created trees use. Simulation thread only.</summary>
    /// <param name="failures">Where to count the prefabs that can't be read, such as a missing asset's.</param>
    public TreePrefabsSectionContent ReadPrefabs(ObjectFailures failures)
    {
        var content = new TreePrefabsSectionContent();
        foreach (ushort index in UsedPrefabIndices())
        {
            try
            {
                TreeInfo info = PrefabCollection<TreeInfo>.GetPrefab(index);
                if (info == null)
                {
                    failures.Add(PrefabFields.Describe<TreeInfo>("Prefab", index, index) + " isn't loaded, so its trees have no prefab.");
                    continue;
                }

                TreePrefabRecord record = ReadPrefab(index, info);
                string? problem = TreePrefabsSectionWriter.Check(record);
                if (problem is not null)
                {
                    failures.Add(PrefabFields.Describe<TreeInfo>("Prefab", index, index) + " can't be written: " + problem + ".");
                    continue;
                }

                content.Prefabs.Add(record);
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<TreeInfo>("Prefab", index, index) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    // The tree is a copy, so nothing here can change it.
    private static TreeRecord ReadTree(uint id, TreeInstance tree)
    {
        TreeInfo info = tree.Info;
        if (info == null)
        {
            throw new InvalidOperationException("its prefab isn't loaded");
        }

        // As TreeInstance.RenderInstance: the randomizer seeded with the id (its uint overload), the scale first, then
        // the brightness (docs/research/cs1-assets.md, Placement).
        var randomizer = new Randomizer(id);
        float scale = info.m_minScale + (randomizer.Int32(RandomSteps) * (info.m_maxScale - info.m_minScale) * RandomStep);
        float brightness = info.m_minBrightness + (randomizer.Int32(RandomSteps) * (info.m_maxBrightness - info.m_minBrightness) * RandomStep);
        return new TreeRecord
        {
            Id = id,
            Prefab = tree.m_infoIndex,
            Position = PrefabFields.ToDump(tree.Position),
            Scale = scale,
            Brightness = brightness,
            Flags = tree.m_flags,
        };
    }

    private static TreePrefabRecord ReadPrefab(ushort index, TreeInfo info) => new()
    {
        Index = index,
        Name = info.name ?? string.Empty,
        Class = PrefabFields.ToDump(info.m_class),
        Mesh = PrefabFields.MeshBounds(info.m_mesh),
        MinScale = info.m_minScale,
        MaxScale = info.m_maxScale,
        MinBrightness = info.m_minBrightness,
        MaxBrightness = info.m_maxBrightness,
        DefaultColor = PrefabFields.ToDump(info.m_defaultColor),
        CreateRuining = info.m_createRuining,
    };

    private List<ushort> UsedPrefabIndices()
    {
        var used = new Dictionary<ushort, bool>();
        TreeInstance[] trees = _trees.m_trees.m_buffer;
        for (int i = 0; i < trees.Length; i++)
        {
            if (((TreeInstance.Flags)trees[i].m_flags & TreeInstance.Flags.Created) != 0)
            {
                used[trees[i].m_infoIndex] = true;
            }
        }

        var indices = new List<ushort>(used.Keys);
        indices.Sort();
        return indices;
    }
}
