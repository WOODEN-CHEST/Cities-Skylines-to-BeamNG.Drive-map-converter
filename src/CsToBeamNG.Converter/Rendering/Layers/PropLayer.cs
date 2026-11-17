using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Props;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the props placed on their own as magenta dots, and zoomed in as their mesh's footprint, scaled and turned as
/// the game turns them, so their angles show; decals lighter. Markers, which the game shows only in the editors, are
/// left out.
/// </summary>
public sealed class PropLayer : IRenderLayer
{
    /// <summary>Footprints at least this many pixels across are drawn as such; smaller props as one pixel.</summary>
    public const double MinFootprintPixels = 2;

    /// <summary>How opaque decals are over what lies below.</summary>
    public const byte DecalOpacity = 140;

    /// <summary>The colour of props.</summary>
    public static Rgb PropColor { get; } = new(208, 64, 168);

    /// <summary>The colour of decals.</summary>
    public static Rgb DecalColor { get; } = new(236, 156, 212);

    /// <inheritdoc/>
    public string Name => "props";

    /// <inheritdoc/>
    public string Description =>
        "props as magenta dots, zoomed in as their meshes' footprints turned by their angles; decals pale pink; markers and props the game doesn't draw are left out";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityProps>() is not { } props)
        {
            return;
        }

        CityPropPrefabs? prefabs = context.World.Find<CityPropPrefabs>();
        MapCanvas canvas = context.Canvas;
        RenderView view = canvas.View;
        Span<(double X, double Z)> footprint = stackalloc (double X, double Z)[4];
        foreach (PropRecord prop in props.Props)
        {
            if (!prop.IsDrawn)
            {
                continue;
            }

            PropPrefabRecord? prefab = null;
            if (prefabs is not null && prefabs.TryGet(prop.Prefab, out PropPrefabRecord? found))
            {
                prefab = found;
                if (found.Marker)
                {
                    continue;
                }
            }

            bool decal = prefab is { Decal: true };
            Rgb color = decal ? DecalColor : PropColor;
            if (prefab?.Mesh is not { } mesh || mesh.Max.X < mesh.Min.X || mesh.Max.Z < mesh.Min.Z
                || Math.Max(mesh.Max.X - (double)mesh.Min.X, mesh.Max.Z - (double)mesh.Min.Z) * prop.Scale / view.MetersPerPixel < MinFootprintPixels)
            {
                canvas.SetPixel((int)Math.Floor(view.ToPixelX(prop.Position.X)), (int)Math.Floor(view.ToPixelY(prop.Position.Z)), color);
                continue;
            }

            var frame = new ObjectFrame(prop.Position, prop.Angle);
            double scale = prop.Scale;
            footprint[0] = frame.ToWorld(mesh.Min.X * scale, mesh.Min.Z * scale);
            footprint[1] = frame.ToWorld(mesh.Max.X * scale, mesh.Min.Z * scale);
            footprint[2] = frame.ToWorld(mesh.Max.X * scale, mesh.Max.Z * scale);
            footprint[3] = frame.ToWorld(mesh.Min.X * scale, mesh.Max.Z * scale);
            if (decal)
            {
                canvas.BlendPolygon(footprint, color, DecalOpacity);
            }
            else
            {
                canvas.FillPolygon(footprint, color);
            }
        }
    }
}
