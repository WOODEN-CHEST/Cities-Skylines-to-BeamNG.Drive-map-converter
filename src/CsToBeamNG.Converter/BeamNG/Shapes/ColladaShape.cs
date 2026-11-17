using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>
/// A mesh file as the game expects it: levels of detail of one visible mesh, and optionally a collision mesh
/// (docs/research/beamng-level-format.md, Meshes).
/// </summary>
public sealed class ColladaShape
{
    private readonly string _name = string.Empty;
    private readonly IReadOnlyList<ShapeDetail> _details = [];

    /// <summary>
    /// The name of the visible mesh. Its levels of detail become the nodes <c>&lt;name&gt;_a&lt;pixel size&gt;</c>.
    /// </summary>
    public required string Name
    {
        get => _name;
        init
        {
            BeamNGNames.ThrowIfNotSceneName(value, nameof(Name));
            _name = value;
        }
    }

    /// <summary>The levels of detail, at least one, with distinct pixel sizes.</summary>
    public required IReadOnlyList<ShapeDetail> Details
    {
        get => _details;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("A shape needs at least one level of detail.", nameof(Details));
            }

            var sizes = new HashSet<int>();
            foreach (ShapeDetail detail in value)
            {
                if (!sizes.Add(detail.PixelSize))
                {
                    throw new ArgumentException($"Two levels of detail have the pixel size {detail.PixelSize}.", nameof(Details));
                }
            }

            _details = [.. value];
        }
    }

    /// <summary>The mesh that vehicles collide with, or <see langword="null"/> for none.</summary>
    public ShapeMesh? CollisionMesh { get; init; }
}
