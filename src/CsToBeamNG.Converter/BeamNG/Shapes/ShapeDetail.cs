using System;

namespace CsToBeamNG.Converter.BeamNG.Shapes;

/// <summary>A level of detail of a <see cref="ColladaShape"/>.</summary>
public sealed record ShapeDetail
{
    /// <summary>Creates a level of detail.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="pixelSize">
    /// The size on screen, in pixels, from which this level is used (docs/research/beamng-level-format.md, Meshes).
    /// </param>
    public ShapeDetail(ShapeMesh mesh, int pixelSize)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelSize);
        Mesh = mesh;
        PixelSize = pixelSize;
    }

    /// <summary>The mesh.</summary>
    public ShapeMesh Mesh { get; }

    /// <summary>The size on screen, in pixels, from which this level is used.</summary>
    public int PixelSize { get; }
}
