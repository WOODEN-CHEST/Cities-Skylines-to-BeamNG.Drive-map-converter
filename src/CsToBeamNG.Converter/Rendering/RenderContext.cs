using System;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Rendering;

/// <summary>What a render layer draws from and onto.</summary>
public sealed class RenderContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="world">The world model.</param>
    /// <param name="areaOfInterest">The planned area of interest.</param>
    /// <param name="canvas">The canvas to draw on.</param>
    public RenderContext(CityWorld world, AreaOfInterest areaOfInterest, MapCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(areaOfInterest);
        ArgumentNullException.ThrowIfNull(canvas);
        World = world;
        AreaOfInterest = areaOfInterest;
        Canvas = canvas;
    }

    /// <summary>The world model.</summary>
    public CityWorld World { get; }

    /// <summary>The planned area of interest.</summary>
    public AreaOfInterest AreaOfInterest { get; }

    /// <summary>The canvas to draw on.</summary>
    public MapCanvas Canvas { get; }
}
