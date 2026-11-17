using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>Draws the map's 9 × 9 tile grid and tints the tiles the city has unlocked.</summary>
public sealed class TileGridLayer : IRenderLayer
{
    /// <summary>The opacity of the tint over unlocked tiles, 0–255; light, so the terrain and water below stay readable.</summary>
    public const byte UnlockedAlpha = 40;

    /// <summary>The colour of the tile borders.</summary>
    public static Rgb GridColor { get; } = new(150, 145, 130);

    /// <summary>The tint over unlocked tiles.</summary>
    public static Rgb UnlockedTint { get; } = new(110, 190, 80);

    /// <summary>The colour of the unlocked tiles' borders.</summary>
    public static Rgb UnlockedEdgeColor { get; } = new(60, 130, 40);

    /// <inheritdoc/>
    public string Name => "tiles";

    /// <inheritdoc/>
    public string Description => "the 9 × 9 tile grid in grey; unlocked tiles tinted green with a green border";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MapCanvas canvas = context.Canvas;
        for (int z = 0; z < MapGeometry.TilesPerSide; z++)
        {
            for (int x = 0; x < MapGeometry.TilesPerSide; x++)
            {
                canvas.StrokeRect(MapGeometry.TileBounds(new TileCoordinate(x, z)), GridColor, 1);
            }
        }

        foreach (TileCoordinate tile in context.World.UnlockedTiles)
        {
            MapRect bounds = MapGeometry.TileBounds(tile);
            canvas.BlendRect(bounds, UnlockedTint, UnlockedAlpha);
            canvas.StrokeRect(bounds, UnlockedEdgeColor, 2);
        }
    }
}
