using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// An endless water surface at one height, such as the sea: everything below it is under water, to the horizon
/// (docs/research/beamng-level-format.md, Water). Official levels place it at x = y = 0.
/// </summary>
public sealed class WaterPlane : SceneObject
{
    private readonly float _height;

    /// <inheritdoc/>
    public override string ClassName => "WaterPlane";

    /// <summary>The height of the water surface: BeamNG's z, in metres.</summary>
    public required float Height
    {
        get => _height;
        init => _height = float.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(Height), value, "The height must be finite.");
    }

    /// <summary>How the water looks.</summary>
    public required WaterStyle Style { get; init; }

    internal override Vector3? ScenePosition => new Vector3(0, 0, Height);

    internal override void AddFields(SceneFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        fields.SetAll(Style.Fields);
    }
}
