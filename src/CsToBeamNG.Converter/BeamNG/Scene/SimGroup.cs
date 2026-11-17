using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// A group of scene objects. The game loads a group's children from the folder that has the group's name, next
/// to the file that declares the group, so the name is also a folder name.
/// </summary>
public sealed class SimGroup : SceneObject
{
    /// <summary>Creates an empty group.</summary>
    /// <param name="name">The group's name; see <see cref="BeamNGNames.IsSceneName"/>.</param>
    [SetsRequiredMembers]
    public SimGroup(string name)
    {
        Name = name;
        Key = "group:" + name;
    }

    /// <inheritdoc/>
    public override string ClassName => "SimGroup";

    /// <summary>The objects and groups in this group, in the order they are written.</summary>
    public IList<SceneObject> Children { get; } = [];

    internal override void AddFields(SceneFields fields)
    {
    }
}
