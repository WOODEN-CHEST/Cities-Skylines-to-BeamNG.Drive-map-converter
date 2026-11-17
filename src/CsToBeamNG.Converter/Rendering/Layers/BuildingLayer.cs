using System;
using CsToBeamNG.Converter.Buildings;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the buildings' lots (<see cref="BuildingLot"/>) as rectangles coloured by <see cref="BuildingCategory"/>, with an
/// outline and, zoomed in, the front edge that faces the road marked dark, so the render shows where each building
/// stands and which way it faces. Buildings whose class isn't on the default layer, such as water pipe junctions and
/// markers, are left out, as the game's normal view leaves them out.
/// </summary>
public sealed class BuildingLayer : IRenderLayer
{
    /// <summary>Lots smaller than this many pixels across are drawn as one pixel at the building's position.</summary>
    public const double MinLotPixels = 1.5;

    /// <summary>Lots at least this many pixels across get an outline.</summary>
    public const double MinOutlinePixels = 4;

    /// <summary>Up to how many metres per pixel the front edge is marked.</summary>
    public const double FrontEdgeMetersPerPixel = 1;

    /// <summary>The colour of residential buildings.</summary>
    public static Rgb ResidentialColor { get; } = new(112, 190, 92);

    /// <summary>The colour of commercial buildings.</summary>
    public static Rgb CommercialColor { get; } = new(84, 140, 228);

    /// <summary>The colour of industrial buildings: brick red rather than the zone's yellow, which arterial roads use.</summary>
    public static Rgb IndustrialColor { get; } = new(184, 92, 72);

    /// <summary>The colour of office buildings.</summary>
    public static Rgb OfficeColor { get; } = new(96, 208, 220);

    /// <summary>The colour of all other buildings: services, parks, monuments, transport, decorations.</summary>
    public static Rgb OtherColor { get; } = new(176, 136, 208);

    /// <summary>The colour of buildings drawn without their prefabs, when the dump has no <c>building-prefabs</c> section.</summary>
    public static Rgb UnknownColor { get; } = new(150, 150, 150);

    /// <summary>The colour of outlines.</summary>
    public static Rgb OutlineColor { get; } = new(64, 64, 64);

    /// <summary>The colour of the front edge.</summary>
    public static Rgb FrontColor { get; } = new(16, 16, 16);

    /// <inheritdoc/>
    public string Name => "buildings";

    /// <inheritdoc/>
    public string Description =>
        "building lots coloured by zone: residential green, commercial blue, industrial brick red, office cyan, others violet (grey without prefabs), outlined; "
        + "at 1 m per pixel or less the front edge, which faces the road, dark; buildings the game doesn't draw and those off the default layer, "
        + "such as water pipe junctions and markers, are left out";

    /// <summary>Returns the colour of a category.</summary>
    /// <param name="category">The category.</param>
    public static Rgb ColorOf(BuildingCategory category) => category switch
    {
        BuildingCategory.Residential => ResidentialColor,
        BuildingCategory.Commercial => CommercialColor,
        BuildingCategory.Industrial => IndustrialColor,
        BuildingCategory.Office => OfficeColor,
        BuildingCategory.Other => OtherColor,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown building category."),
    };

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityBuildings>() is not { } buildings)
        {
            return;
        }

        CityBuildingPrefabs? prefabs = context.World.Find<CityBuildingPrefabs>();
        MapCanvas canvas = context.Canvas;
        RenderView view = canvas.View;
        double metersPerPixel = view.MetersPerPixel;
        Span<(double X, double Z)> corners = stackalloc (double X, double Z)[BuildingLot.CornerCount];
        foreach (BuildingRecord building in buildings.Buildings)
        {
            if (!building.IsDrawn
                || (prefabs is not null && prefabs.TryGet(building.Prefab, out BuildingPrefabRecord? prefab) && (prefab.Class.Layer & DumpItemClass.DefaultLayer) == 0))
            {
                continue;
            }

            Rgb color = prefabs is null ? UnknownColor : ColorOf(prefabs.CategoryOf(building.Prefab));
            double sizePixels = Math.Min(BuildingLot.WidthMeters(building), BuildingLot.LengthMeters(building)) / metersPerPixel;
            if (sizePixels < MinLotPixels)
            {
                canvas.SetPixel((int)Math.Floor(view.ToPixelX(building.Position.X)), (int)Math.Floor(view.ToPixelY(building.Position.Z)), color);
                continue;
            }

            BuildingLot.Corners(building, corners);
            canvas.FillPolygon(corners, color);
            if (sizePixels >= MinOutlinePixels)
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    (double X, double Z) from = corners[i];
                    (double X, double Z) to = corners[(i + 1) % corners.Length];
                    canvas.DrawLine(from.X, from.Z, to.X, to.Z, OutlineColor);
                }
            }

            if (metersPerPixel <= FrontEdgeMetersPerPixel)
            {
                canvas.DrawLine(corners[0].X, corners[0].Z, corners[1].X, corners[1].Z, FrontColor);
            }
        }
    }
}
