using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>An object of a level's scene tree: one line of an <c>items.level.json</c> file.</summary>
/// <remarks>
/// Every object has a <see cref="Key"/>, from which its <c>persistentId</c> derives
/// (<see cref="PersistentId.ForObject"/>), and optionally a <see cref="Name"/>. Objects are immutable once built,
/// except for the contents of <see cref="SimGroup.Children"/> and <see cref="GenericSceneObject.Fields"/>; don't
/// change those after <see cref="SceneTreeWriter.CreateFiles"/>.
/// </remarks>
public abstract class SceneObject
{
    private readonly string? _name;

    private protected SceneObject()
    {
    }

    /// <summary>The class the game creates, such as <c>TSStatic</c>.</summary>
    public abstract string ClassName { get; }

    /// <summary>
    /// A key that is unique within the level and stays the same when the same input is converted again, such as
    /// <c>network/segment/1234</c>. The object's <c>persistentId</c> derives from it.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The object's name, or <see langword="null"/>. Names are unique within a level, ignoring case; the game looks
    /// some objects up by name. See <see cref="BeamNGNames.IsSceneName"/>.
    /// </summary>
    public string? Name
    {
        get => _name;
        init
        {
            if (value is not null)
            {
                BeamNGNames.ThrowIfNotSceneName(value, nameof(Name));
            }

            _name = value;
        }
    }

    /// <summary>The <c>position</c> field, which comes right after <c>__parent</c>, or <see langword="null"/> if the object has none.</summary>
    internal virtual Vector3? ScenePosition => null;

    /// <summary>Adds the class-specific fields: everything except name, class, persistentId, __parent and position.</summary>
    internal abstract void AddFields(SceneFields fields);
}
