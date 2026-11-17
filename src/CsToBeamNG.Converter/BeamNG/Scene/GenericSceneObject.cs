using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>A scene object of any class, with its fields given as data. Used for objects without a dedicated type.</summary>
public sealed class GenericSceneObject : SceneObject
{
    /// <summary>Creates an object without fields.</summary>
    /// <param name="className">The class the game creates, such as <c>ScatterSky</c>.</param>
    public GenericSceneObject(string className)
    {
        BeamNGNames.ThrowIfNotSceneName(className, nameof(className));
        ClassName = className;
    }

    /// <inheritdoc/>
    public override string ClassName { get; }

    /// <summary>The <c>position</c> field, or <see langword="null"/> to write none.</summary>
    public Vector3? Position { get; init; }

    /// <summary>The other fields.</summary>
    public SceneFields Fields { get; } = new();

    internal override Vector3? ScenePosition => Position;

    internal override void AddFields(SceneFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        fields.SetAll(Fields);
    }
}
