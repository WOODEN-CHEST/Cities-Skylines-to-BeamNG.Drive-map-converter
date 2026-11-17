using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Rendering.Layers;

/// <summary>
/// Draws the networks like a road map: each segment as a band of its prefab's width along its centre curve, coloured
/// by <see cref="NetCategory"/>, with a dark casing; tunnels faintly underneath. Zoomed in, it adds one-way arrows and
/// the line between the corners the game computed at each segment end, which outline the junctions.
/// </summary>
public sealed class NetworkLayer : IRenderLayer
{
    /// <summary>Up to how many metres per pixel the corner lines are drawn.</summary>
    public const double CornerLinesMetersPerPixel = 1;

    /// <summary>Bands narrower than this many pixels are drawn as one-pixel lines.</summary>
    public const double MinBandPixels = 1.5;

    /// <summary>Bands at least this many pixels wide get a casing.</summary>
    public const double MinCasingPixels = 3;

    /// <summary>One-way arrows are drawn on bands at least this many pixels wide...</summary>
    public const double MinArrowWidthPixels = 6;

    /// <summary>...and on segments at least this many pixels long.</summary>
    public const double MinArrowLengthPixels = 24;

    /// <summary>How opaque tunnels are over the ground.</summary>
    public const byte TunnelOpacity = 80;

    // The longest straight piece of a band, in pixels, and the most pieces per segment.
    private const double PiecePixels = 6;
    private const int MaxPieces = 64;

    /// <summary>The colour of highways.</summary>
    public static Rgb HighwayColor { get; } = new(232, 128, 40);

    /// <summary>The colour of arterial roads.</summary>
    public static Rgb ArterialColor { get; } = new(244, 200, 64);

    /// <summary>The colour of local roads.</summary>
    public static Rgb LocalColor { get; } = new(252, 252, 248);

    /// <summary>The colour of paths.</summary>
    public static Rgb PathColor { get; } = new(196, 156, 104);

    /// <summary>The colour of tracks.</summary>
    public static Rgb RailColor { get; } = new(104, 92, 128);

    /// <summary>The colour of the casings around the bands.</summary>
    public static Rgb CasingColor { get; } = new(56, 56, 56);

    /// <summary>The colour of one-way arrows and corner lines.</summary>
    public static Rgb MarkColor { get; } = new(24, 24, 24);

    /// <summary>The colour of segments drawn without their prefabs, when the dump has no <c>net-prefabs</c> section.</summary>
    public static Rgb UnknownColor { get; } = new(160, 160, 160);

    /// <inheritdoc/>
    public string Name => "networks";

    /// <inheritdoc/>
    public string Description =>
        "segments as bands of their width with a dark casing: highways orange, arterial roads yellow, local roads white, paths tan, tracks purple; "
        + "tunnels faint; one-way arrows on wide enough segments; at 1 m per pixel or less the lines between the game's corners at segment ends; "
        + "pipes, power lines, transport lines and ship and air routes aren't drawn";

    /// <summary>Returns the colour of a category; <see cref="NetCategory.Other"/> isn't drawn and has none.</summary>
    /// <param name="category">A category other than <see cref="NetCategory.Other"/>.</param>
    public static Rgb ColorOf(NetCategory category) => category switch
    {
        NetCategory.Highway => HighwayColor,
        NetCategory.Arterial => ArterialColor,
        NetCategory.Local => LocalColor,
        NetCategory.Path => PathColor,
        NetCategory.Rail => RailColor,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "This category isn't drawn."),
    };

    /// <inheritdoc/>
    public void Draw(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.World.Find<CityNetSegments>() is not { } segments)
        {
            return;
        }

        MapCanvas canvas = context.Canvas;
        if (context.World.Find<CityNetPrefabs>() is not { } prefabs)
        {
            foreach (NetSegmentRecord segment in segments.Segments)
            {
                DrawCurve(canvas, segment.Bezier, UnknownColor);
            }

            return;
        }

        List<Item> items = Collect(segments, prefabs, context.World.Find<CityNetNodes>());
        double metersPerPixel = canvas.View.MetersPerPixel;
        foreach (Item item in items)
        {
            if (item.Underground)
            {
                DrawBand(canvas, item, item.Prefab.HalfWidth, ColorOf(item.Category), TunnelOpacity);
            }
        }

        foreach (Item item in items)
        {
            if (!item.Underground && 2 * item.Prefab.HalfWidth / metersPerPixel >= MinCasingPixels)
            {
                DrawBand(canvas, item, item.Prefab.HalfWidth + metersPerPixel, CasingColor, byte.MaxValue);
            }
        }

        foreach (Item item in items)
        {
            if (!item.Underground)
            {
                DrawBand(canvas, item, item.Prefab.HalfWidth, ColorOf(item.Category), byte.MaxValue);
            }
        }

        foreach (Item item in items)
        {
            if (!item.Underground)
            {
                DrawMarks(canvas, item);
            }
        }
    }

    // The segments to draw, tunnels first, then by category from paths to highways, so major roads lie on top; ties by
    // id. A segment is underground if its prefab says so or both its nodes lie underground.
    private static List<Item> Collect(CityNetSegments segments, CityNetPrefabs prefabs, CityNetNodes? nodes)
    {
        var items = new List<Item>();
        foreach (NetSegmentRecord segment in segments.Segments)
        {
            NetCategory category = prefabs.CategoryOf(segment.Prefab);
            if (category == NetCategory.Other || !prefabs.TryGet(segment.Prefab, out NetPrefabRecord? prefab))
            {
                continue;
            }

            bool underground = prefab.Underground
                || (nodes is not null
                    && nodes.TryGet(segment.StartNode, out NetNodeRecord? start) && start.HasFlags(NetNodeRecord.UndergroundFlag)
                    && nodes.TryGet(segment.EndNode, out NetNodeRecord? end) && end.HasFlags(NetNodeRecord.UndergroundFlag));
            items.Add(new Item(segment, prefab, category, underground));
        }

        items.Sort((a, b) =>
            a.Underground != b.Underground ? (a.Underground ? -1 : 1)
            : a.Category != b.Category ? a.Category.CompareTo(b.Category)
            : a.Segment.Id.CompareTo(b.Segment.Id));
        return items;
    }

    // A band of the given half width along the centre curve, as quadrilaterals between samples; a one-pixel line where
    // the band would be too narrow to see.
    private static void DrawBand(MapCanvas canvas, Item item, double halfWidthMeters, Rgb color, byte alpha)
    {
        double metersPerPixel = canvas.View.MetersPerPixel;
        DumpBezier3 curve = item.Segment.Bezier;
        if (2 * halfWidthMeters / metersPerPixel < MinBandPixels)
        {
            DrawCurve(canvas, curve, color);
            return;
        }

        int pieces = Pieces(curve, metersPerPixel);
        Span<(double X, double Z)> quad = stackalloc (double X, double Z)[4];
        if (!TrySample(curve, 0, out (double X, double Z) previous, out (double X, double Z) previousNormal))
        {
            return;
        }

        for (int i = 1; i <= pieces; i++)
        {
            if (!TrySample(curve, (double)i / pieces, out (double X, double Z) point, out (double X, double Z) normal))
            {
                return;
            }

            quad[0] = (previous.X + (previousNormal.X * halfWidthMeters), previous.Z + (previousNormal.Z * halfWidthMeters));
            quad[1] = (point.X + (normal.X * halfWidthMeters), point.Z + (normal.Z * halfWidthMeters));
            quad[2] = (point.X - (normal.X * halfWidthMeters), point.Z - (normal.Z * halfWidthMeters));
            quad[3] = (previous.X - (previousNormal.X * halfWidthMeters), previous.Z - (previousNormal.Z * halfWidthMeters));
            if (alpha == byte.MaxValue)
            {
                canvas.FillPolygon(quad, color);
            }
            else
            {
                canvas.BlendPolygon(quad, color, alpha);
            }

            previous = point;
            previousNormal = normal;
        }
    }

    private static void DrawCurve(MapCanvas canvas, DumpBezier3 curve, Rgb color)
    {
        int pieces = Pieces(curve, canvas.View.MetersPerPixel);
        (double X, double Y, double Z) previous = NetGeometry.Position(curve, 0);
        for (int i = 1; i <= pieces; i++)
        {
            (double X, double Y, double Z) point = NetGeometry.Position(curve, (double)i / pieces);
            canvas.DrawLine(previous.X, previous.Z, point.X, point.Z, color);
            previous = point;
        }
    }

    // One-way arrows in the middle of the segment, and the lines between the corners at its ends.
    private static void DrawMarks(MapCanvas canvas, Item item)
    {
        double metersPerPixel = canvas.View.MetersPerPixel;
        NetSegmentRecord segment = item.Segment;
        if (metersPerPixel <= CornerLinesMetersPerPixel)
        {
            DrawLine(canvas, segment.StartLeft.Position, segment.StartRight.Position);
            DrawLine(canvas, segment.EndLeft.Position, segment.EndRight.Position);
        }

        NetOneWay oneWay = NetDirections.OneWayOf(segment, item.Prefab);
        if (oneWay == NetOneWay.None
            || 2 * item.Prefab.HalfWidth / metersPerPixel < MinArrowWidthPixels
            || NetGeometry.ArcLength(segment.Bezier) / metersPerPixel < MinArrowLengthPixels
            || !TrySample(segment.Bezier, 0.5, out (double X, double Z) middle, out (double X, double Z) normal))
        {
            return;
        }

        // The direction along the curve is the normal turned back by a quarter turn; the arrow points the way traffic goes.
        double sign = oneWay == NetOneWay.StartToEnd ? 1 : -1;
        (double X, double Z) along = (normal.Z * sign, -normal.X * sign);
        double size = Math.Min(1.2 * item.Prefab.HalfWidth, 8 * metersPerPixel);
        (double X, double Z) tip = (middle.X + (along.X * size / 2), middle.Z + (along.Z * size / 2));
        (double X, double Z) back = (tip.X - (along.X * size), tip.Z - (along.Z * size));
        canvas.DrawLine(tip.X, tip.Z, back.X + (normal.X * size * 0.6), back.Z + (normal.Z * size * 0.6), MarkColor);
        canvas.DrawLine(tip.X, tip.Z, back.X - (normal.X * size * 0.6), back.Z - (normal.Z * size * 0.6), MarkColor);
    }

    private static void DrawLine(MapCanvas canvas, DumpVector3 from, DumpVector3 to) =>
        canvas.DrawLine(from.X, from.Z, to.X, to.Z, MarkColor);

    private static int Pieces(DumpBezier3 curve, double metersPerPixel) =>
        Math.Clamp((int)Math.Ceiling(NetGeometry.ArcLength(curve) / metersPerPixel / PiecePixels), 1, MaxPieces);

    // A point of the curve and its horizontal left normal, (−dZ, dX) of the direction; at a point where the curve
    // doesn't move, the direction of its chord. False if neither has a direction.
    private static bool TrySample(DumpBezier3 curve, double t, out (double X, double Z) point, out (double X, double Z) normal)
    {
        (double x, _, double z) = NetGeometry.Position(curve, t);
        point = (x, z);
        (double dx, _, double dz) = NetGeometry.Derivative(curve, t);
        double length = Math.Sqrt((dx * dx) + (dz * dz));
        if (length < 1e-6)
        {
            dx = (double)curve.D.X - curve.A.X;
            dz = (double)curve.D.Z - curve.A.Z;
            length = Math.Sqrt((dx * dx) + (dz * dz));
        }

        if (length < 1e-6)
        {
            normal = default;
            return false;
        }

        normal = (-dz / length, dx / length);
        return true;
    }

    private sealed record Item(NetSegmentRecord Segment, NetPrefabRecord Prefab, NetCategory Category, bool Underground);
}
