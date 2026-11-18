using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// A box of water, such as a lake: the water surface at its position and water down to <see cref="Depth"/> below it
/// (docs/research/beamng-level-format.md, Water).
/// </summary>
/// <remarks>
/// <para>
/// The game centres the box on its position, <c>scale</c> wide, long and high, and fills only its lower half with
/// water: the surface is at the position's z, and water reaches <c>scale</c>'s z / 2 below it (verified in
/// BeamNG.drive 0.40.5 with a vehicle's water test). So the box is written twice as high as <see cref="Depth"/>.
/// </para>
/// <para>
/// The game lays the surface in whole squares of <see cref="GridElementSize"/> and draws nothing of a remainder at the
/// box's edge, so the width and length should be multiples of it.
/// </para>
/// </remarks>
public sealed class WaterBlock : SceneObject
{
    private readonly float _gridElementSize = 1;

    /// <inheritdoc/>
    public override string ClassName => "WaterBlock";

    /// <summary>The middle of the water surface, in BeamNG coordinates.</summary>
    public required Vector3 SurfaceCentre { get; init; }

    /// <summary>The surface's extent along BeamNG's X, in metres.</summary>
    public required float Width { get; init; }

    /// <summary>The surface's extent along BeamNG's Y, in metres.</summary>
    public required float Length { get; init; }

    /// <summary>How far the water reaches below the surface, in metres.</summary>
    public required float Depth { get; init; }

    /// <summary>The size of the squares the surface is made of, in metres.</summary>
    public float GridElementSize
    {
        get => _gridElementSize;
        init => _gridElementSize = value > 0 && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(GridElementSize), value, "The grid element size must be positive.");
    }

    /// <summary>How the water looks.</summary>
    public required WaterStyle Style { get; init; }

    internal override Vector3? ScenePosition => SurfaceCentre;

    internal override void AddFields(SceneFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (!(Width > 0 && Length > 0 && Depth > 0) || !float.IsFinite(Width) || !float.IsFinite(Length) || !float.IsFinite(Depth))
        {
            throw new InvalidOperationException($"Water block '{Key}' needs a positive, finite size.");
        }

        fields.SetAll(Style.Fields);
        fields.Set("gridElementSize", GridElementSize);
        fields.Set("rotationMatrix", RotationMatrix.Identity.ToJson());
        fields.Set("scale", new Vector3(Width, Length, 2 * Depth));
    }
}
