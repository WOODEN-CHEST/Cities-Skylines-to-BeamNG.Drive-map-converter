using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Trees;

/// <summary>Loads the <c>trees</c> section into a <see cref="CityTrees"/> (docs/dump-format.md, <c>trees</c>).</summary>
public sealed class TreesLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => TreesSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => TreesSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A tree with a value that isn't finite, or whose id was listed already, is skipped with a warning. Trees whose
    /// prefab the <c>tree-prefabs</c> section lacks are kept and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        TreesSectionContent content = TreesSectionReader.Read(section);
        CityTreePrefabs? prefabs = context.Find<CityTreePrefabs>();
        var unknownPrefabs = new MissingReferences("trees have a prefab that the tree-prefabs section lacks");
        var trees = new List<TreeRecord>(content.Trees.Count);
        var ids = new HashSet<uint>();
        foreach (TreeRecord tree in content.Trees.OrderBy(tree => tree.Id))
        {
            if (!tree.Position.IsFinite || !float.IsFinite(tree.Scale) || !float.IsFinite(tree.Brightness))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Tree {tree.Id} has a position, scale or brightness that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(tree.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Tree {tree.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (prefabs is not null && !prefabs.TryGet(tree.Prefab, out _))
            {
                unknownPrefabs.Add(string.Create(CultureInfo.InvariantCulture, $"tree {tree.Id} (prefab {tree.Prefab})"));
            }

            trees.Add(tree);
        }

        unknownPrefabs.Report(context);
        return new CityTrees(trees);
    }
}
