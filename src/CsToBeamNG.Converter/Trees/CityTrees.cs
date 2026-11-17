using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Trees;

/// <summary>
/// The trees of the city and the map, by id, in the game's coordinates (docs/dump-format.md, <c>trees</c>;
/// docs/research/cs1-assets.md, Placement).
/// </summary>
public sealed class CityTrees : ISectionData
{
    private readonly Dictionary<uint, TreeRecord> _byId = [];

    /// <summary>Creates the trees.</summary>
    /// <param name="trees">The trees, ordered by id, each id once, with finite values.</param>
    /// <exception cref="ArgumentException">An id is listed twice.</exception>
    public CityTrees(IReadOnlyList<TreeRecord> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);
        foreach (TreeRecord tree in trees)
        {
            if (!_byId.TryAdd(tree.Id, tree))
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Tree {tree.Id} is listed twice."), nameof(trees));
            }
        }

        Trees = trees;
    }

    /// <summary>The trees, ordered by id.</summary>
    public IReadOnlyList<TreeRecord> Trees { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SectionStatistic> Statistics => BuildStatistics();

    /// <inheritdoc/>
    /// <remarks>Trees don't bound the area of interest (docs/architecture.md, D35): the map's forests cover the whole map.</remarks>
    public MapRect? AreaOfInterestBounds => null;

    /// <summary>Looks up a tree by its id.</summary>
    /// <param name="id">The tree's id.</param>
    /// <param name="tree">The tree, if the dump has it.</param>
    public bool TryGet(uint id, [NotNullWhen(true)] out TreeRecord? tree) => _byId.TryGetValue(id, out tree);

    private SectionStatistic[] BuildStatistics()
    {
        int notDrawn = 0;
        int fixedHeight = 0;
        float minScale = float.PositiveInfinity;
        float maxScale = float.NegativeInfinity;
        float minBrightness = float.PositiveInfinity;
        float maxBrightness = float.NegativeInfinity;
        foreach (TreeRecord tree in Trees)
        {
            notDrawn += tree.IsDrawn ? 0 : 1;
            fixedHeight += tree.HasFlags(TreeRecord.FixedHeightFlag) ? 1 : 0;
            minScale = Math.Min(minScale, tree.Scale);
            maxScale = Math.Max(maxScale, tree.Scale);
            minBrightness = Math.Min(minBrightness, tree.Brightness);
            maxBrightness = Math.Max(maxBrightness, tree.Brightness);
        }

        return
        [
            new("trees", string.Create(CultureInfo.InvariantCulture, $"{Trees.Count}: {notDrawn} not drawn, {fixedHeight} with a fixed height")),
            new("scales", Trees.Count == 0 ? "none" : string.Create(CultureInfo.InvariantCulture, $"{minScale:0.###} to {maxScale:0.###}")),
            new("brightness", Trees.Count == 0 ? "none" : string.Create(CultureInfo.InvariantCulture, $"{minBrightness:0.###} to {maxBrightness:0.###}")),
        ];
    }
}
