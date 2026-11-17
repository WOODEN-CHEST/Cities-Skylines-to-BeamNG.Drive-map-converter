using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the water: blue where the water simulation's cells hold water deep enough for the game to show it, darker
/// where it is deeper, and the water sources as small squares.
/// </summary>
public sealed class WaterLayer : IRenderLayer
{
    /// <summary>
    /// The shallowest water drawn, in metres: the game draws the water surface 20/64 m below the simulated one, so
    /// shallower water stays under the ground (docs/research/cs1-save-and-world.md, Water).
    /// </summary>
    public const double MinVisibleDepthMeters = 20.0 / 64;

    /// <summary>The depth at which the water reaches its darkest colour, in metres.</summary>
    public const double DarkestDepthMeters = 20;

    /// <summary>The colour of shallow water.</summary>
    public static Rgb ShallowColor { get; } = new(86, 156, 214);

    /// <summary>The colour of deep water.</summary>
    public static Rgb DeepColor { get; } = new(18, 52, 118);

    /// <summary>The colour of natural water sources.</summary>
    public static Rgb NaturalSourceColor { get; } = new(0, 230, 255);

    /// <summary>The colour of buildings' water sources, such as pumping stations and sewage outlets.</summary>
    public static Rgb BuildingSourceColor { get; } = new(255, 140, 0);

    /// <summary>The colour of water treatment plants' sources and of sources of unknown type.</summary>
    public static Rgb OtherSourceColor { get; } = new(120, 220, 60);

    // How opaque the water is over the terrain, so its relief still shows faintly.
    private const byte Opacity = 215;

    private const int SourceHalfSizePixels = 3;

    /// <inheritdoc/>
    public string Name => "water";

    /// <inheritdoc/>
    public string Description => "water at least 0.31 m deep in blue, darker down to 20 m; water sources as squares: natural cyan, buildings' orange, water treatment green";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityWater>() is not { } water)
        {
            return;
        }

        MapCanvas canvas = context.Canvas;
        RenderView view = canvas.View;
        double minX = water.Grid.OriginX;
        double minZ = water.Grid.OriginZ;
        double extent = (double)water.Grid.Spacing * (water.Grid.Resolution - 1);
        for (int y = 0; y < view.Height; y++)
        {
            double z = view.ToWorldZ(y + 0.5);
            if (z < minZ || z > minZ + extent)
            {
                continue;
            }

            for (int x = 0; x < view.Width; x++)
            {
                double worldX = view.ToWorldX(x + 0.5);
                if (worldX < minX || worldX > minX + extent)
                {
                    continue;
                }

                double depth = water.SampleDepth(worldX, z);
                if (depth >= MinVisibleDepthMeters)
                {
                    canvas.Image[x, y] = canvas.Image[x, y].Blend(Shade(depth), Opacity);
                }
            }
        }

        foreach (WaterSourceRecord source in water.Sources)
        {
            Rgb color = source.Type switch
            {
                WaterSourceRecord.NaturalType => NaturalSourceColor,
                WaterSourceRecord.FacilityType => BuildingSourceColor,
                _ => OtherSourceColor,
            };
            DrawSource(canvas, source.OutputX, source.OutputZ, color);
            if (source.InputX != source.OutputX || source.InputZ != source.OutputZ)
            {
                DrawSource(canvas, source.InputX, source.InputZ, color);
            }
        }
    }

    private static Rgb Shade(double depth)
    {
        double t = Math.Clamp((depth - MinVisibleDepthMeters) / (DarkestDepthMeters - MinVisibleDepthMeters), 0, 1);
        return new Rgb(Mix(ShallowColor.R, DeepColor.R, t), Mix(ShallowColor.G, DeepColor.G, t), Mix(ShallowColor.B, DeepColor.B, t));
    }

    private static byte Mix(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));

    private static void DrawSource(MapCanvas canvas, double x, double z, Rgb color)
    {
        double pixelX = canvas.View.ToPixelX(x);
        double pixelY = canvas.View.ToPixelY(z);
        if (!(pixelX > -SourceHalfSizePixels - 1 && pixelX < canvas.View.Width + SourceHalfSizePixels + 1
            && pixelY > -SourceHalfSizePixels - 1 && pixelY < canvas.View.Height + SourceHalfSizePixels + 1))
        {
            return;
        }

        int centerX = (int)Math.Floor(pixelX);
        int centerY = (int)Math.Floor(pixelY);
        canvas.FillPixels(centerX - SourceHalfSizePixels - 1, centerY - SourceHalfSizePixels - 1, centerX + SourceHalfSizePixels + 2, centerY + SourceHalfSizePixels + 2, new Rgb(20, 20, 20));
        canvas.FillPixels(centerX - SourceHalfSizePixels, centerY - SourceHalfSizePixels, centerX + SourceHalfSizePixels + 1, centerY + SourceHalfSizePixels + 1, color);
    }
}
