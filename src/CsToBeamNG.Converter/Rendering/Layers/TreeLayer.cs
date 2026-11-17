using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Trees;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the trees as dark green dots, and zoomed in as discs the size of their crowns: the prefab mesh's half extent
/// times the tree's scale.
/// </summary>
public sealed class TreeLayer : IRenderLayer
{
    /// <summary>Crowns with a radius of at least this many pixels are drawn as discs; smaller ones as one pixel.</summary>
    public const double MinDiscPixels = 1.5;

    /// <summary>The crown radius of a tree whose prefab or mesh is unknown, before its scale, in metres.</summary>
    public const double UnknownCrownRadiusMeters = 3;

    // The corners of a regular octagon around the origin, of radius 1; √½ comes from Math.Sqrt, which is exact everywhere.
    private static readonly (double X, double Z)[] _octagon = CreateOctagon();

    /// <summary>The colour of trees.</summary>
    public static Rgb TreeColor { get; } = new(34, 110, 50);

    /// <inheritdoc/>
    public string Name => "trees";

    /// <inheritdoc/>
    public string Description =>
        "trees as dark green dots, zoomed in as discs of their crowns' size (the prefab's mesh times the tree's scale); trees the game doesn't draw are left out";

    /// <summary>Returns the radius of a tree's crown: the average half extent of its prefab's mesh on X and Z, times its scale.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="prefabs">The prefabs; <see langword="null"/> if the dump has none.</param>
    public static double CrownRadiusMeters(TreeRecord tree, CityTreePrefabs? prefabs)
    {
        ArgumentNullException.ThrowIfNull(tree);
        double radius = UnknownCrownRadiusMeters;
        if (prefabs is not null && prefabs.TryGet(tree.Prefab, out TreePrefabRecord? prefab) && prefab.Mesh is { } mesh
            && mesh.Max.X >= mesh.Min.X && mesh.Max.Z >= mesh.Min.Z)
        {
            radius = ((mesh.Max.X - (double)mesh.Min.X) + (mesh.Max.Z - (double)mesh.Min.Z)) / 4;
        }

        return radius * tree.Scale;
    }

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityTrees>() is not { } trees)
        {
            return;
        }

        CityTreePrefabs? prefabs = context.World.Find<CityTreePrefabs>();
        MapCanvas canvas = context.Canvas;
        RenderView view = canvas.View;
        Span<(double X, double Z)> disc = stackalloc (double X, double Z)[_octagon.Length];
        foreach (TreeRecord tree in trees.Trees)
        {
            if (!tree.IsDrawn)
            {
                continue;
            }

            double x = tree.Position.X;
            double z = tree.Position.Z;
            double radius = CrownRadiusMeters(tree, prefabs);
            if (x + radius < view.Area.MinX || x - radius > view.Area.MaxX || z + radius < view.Area.MinZ || z - radius > view.Area.MaxZ)
            {
                continue;
            }

            if (radius / view.MetersPerPixel < MinDiscPixels)
            {
                canvas.SetPixel((int)Math.Floor(view.ToPixelX(x)), (int)Math.Floor(view.ToPixelY(z)), TreeColor);
                continue;
            }

            for (int i = 0; i < _octagon.Length; i++)
            {
                disc[i] = (x + (radius * _octagon[i].X), z + (radius * _octagon[i].Z));
            }

            canvas.FillPolygon(disc, TreeColor);
        }
    }

    private static (double X, double Z)[] CreateOctagon()
    {
        double diagonal = Math.Sqrt(0.5);
        return [(1, 0), (diagonal, diagonal), (0, 1), (-diagonal, diagonal), (-1, 0), (-diagonal, -diagonal), (0, -1), (diagonal, -diagonal)];
    }
}
