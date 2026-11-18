using System;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the terrain: the ground the game draws (<see cref="CityTerrain.SampleGround(double, double)"/>), tinted by height and shaded
/// with light from the north-west, so slopes, embankments and cuts show.
/// </summary>
public sealed class HillshadeLayer : IRenderLayer
{
    // The light comes from the north-west (−X, +Z), 45° above the horizon: a unit vector in the game's axes (Y up).
    private const double LightX = -0.5;
    private const double LightY = 0.70710678118654752;
    private const double LightZ = 0.5;

    // Slopes are steepened for the shading, so gentle terrain still shows relief.
    private const double ReliefExaggeration = 2;

    // The brightness of a surface facing away from the light, and the part the light adds.
    private const double Ambient = 0.45;
    private const double Diffuse = 0.55;

    // Height tints from the lowest to the highest ground of the map.
    private static readonly Rgb[] _tints =
    [
        new(92, 128, 76),
        new(138, 160, 92),
        new(186, 172, 122),
        new(150, 126, 102),
        new(236, 234, 228),
    ];

    /// <inheritdoc/>
    public string Name => "terrain";

    /// <inheritdoc/>
    public string Description => "the ground the game draws (detail heights where a tile has them), shaded with light from the north-west and tinted from green (lowest) through tan and brown to white (highest)";

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityTerrain>() is not { } terrain)
        {
            return;
        }

        MapCanvas canvas = context.Canvas;
        RenderView view = canvas.View;
        double low = terrain.FinalHeights.MinMeters;
        double range = Math.Max(terrain.FinalHeights.MaxMeters - low, 1);
        double step = view.MetersPerPixel;
        int width = view.Width;

        // Heights of the pixel centres in three rolling rows: the one above, the current one and the one below, each
        // with one extra pixel on both sides for the slopes at the image's edges.
        double[] above = new double[width + 2];
        double[] current = new double[width + 2];
        double[] below = new double[width + 2];
        SampleRow(terrain, view, -1, above);
        SampleRow(terrain, view, 0, current);
        for (int y = 0; y < view.Height; y++)
        {
            SampleRow(terrain, view, y + 1, below);
            double z = view.ToWorldZ(y + 0.5);
            for (int x = 0; x < width; x++)
            {
                if (!IsOnMap(view.ToWorldX(x + 0.5), z))
                {
                    continue;
                }

                // Rows run along −Z, so the row above is the +Z neighbour.
                double slopeX = (current[x + 2] - current[x]) / (2 * step) * ReliefExaggeration;
                double slopeZ = (above[x + 1] - below[x + 1]) / (2 * step) * ReliefExaggeration;
                double length = Math.Sqrt((slopeX * slopeX) + 1 + (slopeZ * slopeZ));
                double lit = Math.Max(0, ((-slopeX * LightX) + LightY + (-slopeZ * LightZ)) / length);
                double brightness = Ambient + (Diffuse * lit);
                Rgb tint = Tint((current[x + 1] - low) / range);
                canvas.SetPixel(x, y, new Rgb(Scale(tint.R, brightness), Scale(tint.G, brightness), Scale(tint.B, brightness)));
            }

            (above, current, below) = (current, below, above);
        }
    }

    private static bool IsOnMap(double x, double z) =>
        x >= MapGeometry.Bounds.MinX && x <= MapGeometry.Bounds.MaxX && z >= MapGeometry.Bounds.MinZ && z <= MapGeometry.Bounds.MaxZ;

    private static void SampleRow(CityTerrain terrain, RenderView view, int y, double[] heights)
    {
        double z = view.ToWorldZ(y + 0.5);
        for (int x = -1; x <= view.Width; x++)
        {
            heights[x + 1] = terrain.SampleGround(view.ToWorldX(x + 0.5), z);
        }
    }

    private static Rgb Tint(double fraction)
    {
        double position = Math.Clamp(fraction, 0, 1) * (_tints.Length - 1);
        int index = Math.Min((int)position, _tints.Length - 2);
        double t = position - index;
        Rgb from = _tints[index];
        Rgb to = _tints[index + 1];
        return new Rgb(Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t));
    }

    private static byte Mix(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));

    private static byte Scale(byte channel, double factor) => (byte)Math.Clamp(Math.Round(channel * factor), 0, 255);
}
