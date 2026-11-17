using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Cli;

/// <summary>The part of the map that <c>inspect --render</c> draws, as chosen with <c>--area</c>.</summary>
/// <param name="Text">The value as written on the command line, such as <c>map</c>.</param>
/// <param name="Description">How the report names it, such as "the whole map".</param>
/// <param name="Bounds">The fixed area, or <see langword="null"/> for the area of interest.</param>
internal sealed record RenderArea(string Text, string Description, MapRect? Bounds)
{
    /// <summary>The area of interest, the default.</summary>
    public static RenderArea AreaOfInterest { get; } = new("aoi", "the area of interest", null);

    /// <summary>The whole map.</summary>
    public static RenderArea Map { get; } = new("map", "the whole map", MapGeometry.Bounds);

    /// <summary>Returns the area to draw for a planned area of interest.</summary>
    public MapRect Resolve(AreaOfInterest areaOfInterest) => Bounds ?? areaOfInterest.Bounds;

    /// <summary>Returns the value as written on the command line; the help text shows it as the default.</summary>
    public override string ToString() => Text;
}
