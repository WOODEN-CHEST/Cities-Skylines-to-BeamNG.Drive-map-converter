namespace CsToBeamNG.Converter.Rendering;

/// <summary>
/// One layer of a top-down debug render, such as the tile grid or the roads. A layer draws whatever its data in the
/// world model has, and nothing if that data wasn't loaded.
/// </summary>
public interface IRenderLayer
{
    /// <summary>A short lowercase name, such as <c>tiles</c>.</summary>
    string Name { get; }

    /// <summary>What it draws and in which colours, for the legend <c>cstobeamng inspect</c> prints.</summary>
    string Description { get; }

    /// <summary>Draws the layer.</summary>
    /// <param name="context">The world model, the area of interest and the canvas.</param>
    void Draw(RenderContext context);
}
