using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// A road drawn as a decal along a spline. With a <see cref="Drivability"/> above 0 it is also an AI road, from
/// which the game builds its navigation graph (docs/research/beamng-ai-and-traffic.md).
/// </summary>
public sealed class DecalRoad : SceneObject
{
    /// <summary>The material of invisible roads that exist only for the AI.</summary>
    public const string InvisibleMaterial = "road_invisible";

    private readonly IReadOnlyList<DecalRoadNode> _nodes = [];
    private readonly float _drivability;
    private readonly int? _lanesLeft;
    private readonly int? _lanesRight;

    /// <inheritdoc/>
    public override string ClassName => "DecalRoad";

    /// <summary>The nodes, at least two, in the road's direction.</summary>
    public required IReadOnlyList<DecalRoadNode> Nodes
    {
        get => _nodes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count < 2)
            {
                throw new ArgumentException("A road needs at least two nodes.", nameof(Nodes));
            }

            foreach (DecalRoadNode node in value)
            {
                if (!(node.Width > 0) || !float.IsFinite(node.Width))
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"Road widths must be positive: {node}."), nameof(Nodes));
                }
            }

            _nodes = [.. value];
        }
    }

    /// <summary>The material, such as <see cref="InvisibleMaterial"/>.</summary>
    public required string Material { get; init; }

    /// <summary>How much the AI likes the road: 0 (default) makes it no AI road; higher values are preferred.</summary>
    public float Drivability
    {
        get => _drivability;
        init => _drivability = value >= 0 && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Drivability), value, "Drivability can't be negative.");
    }

    /// <summary>
    /// The number of lanes left of the centre line, looking along the node order, or <see langword="null"/> to let
    /// the game decide. Setting either lane count writes <c>autoLanes: false</c>.
    /// </summary>
    public int? LanesLeft
    {
        get => _lanesLeft;
        init => _lanesLeft = ValidateLaneCount(value, nameof(LanesLeft));
    }

    /// <summary>The number of lanes right of the centre line, or <see langword="null"/> to let the game decide.</summary>
    public int? LanesRight
    {
        get => _lanesRight;
        init => _lanesRight = ValidateLaneCount(value, nameof(LanesRight));
    }

    /// <summary>Whether traffic may only follow the node order (or the reverse, with <see cref="FlipDirection"/>).</summary>
    public bool OneWay { get; init; }

    /// <summary>Whether the road's direction is the reverse of the node order.</summary>
    public bool FlipDirection { get; init; }

    /// <summary>
    /// Whether the road is drawn with the game's improved spline interpolation, or <see langword="null"/> for the
    /// game's default. Official AI roads usually set it.
    /// </summary>
    public bool? ImprovedSpline { get; init; }

    internal override Vector3? ScenePosition => _nodes[0].Position;

    internal override void AddFields(SceneFields fields)
    {
        if (LanesLeft is not null || LanesRight is not null)
        {
            // map.lua reads lanesLeft and lanesRight only when autoLanes is false.
            fields.Set("autoLanes", false);
            fields.Set("lanesLeft", LanesLeft ?? 0);
            fields.Set("lanesRight", LanesRight ?? 0);
        }

        fields.Set("drivability", Drivability);
        if (FlipDirection)
        {
            fields.Set("flipDirection", true);
        }

        if (ImprovedSpline is bool improvedSpline)
        {
            fields.Set("improvedSpline", improvedSpline);
        }

        fields.Set("material", Material);
        fields.Set("nodes", NodesToJson());
        if (OneWay)
        {
            fields.Set("oneWay", true);
        }
    }

    private static int? ValidateLaneCount(int? value, string propertyName) =>
        value is null or >= 0 ? value : throw new ArgumentOutOfRangeException(propertyName, value, "A lane count can't be negative.");

    private JsonArray NodesToJson()
    {
        var nodes = new JsonArray();
        foreach (DecalRoadNode node in _nodes)
        {
            nodes.Add(BeamNGJson.Floats(node.Position.X, node.Position.Y, node.Position.Z, node.Width));
        }

        return nodes;
    }
}
