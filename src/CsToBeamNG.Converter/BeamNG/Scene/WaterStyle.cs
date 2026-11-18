namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// How a water object looks: its colours, textures, reflections, ripples and waves, as the fields that the game's
/// water classes share (<c>WaterPlane</c>, <c>WaterBlock</c>, <c>River</c>). <c>BeamNG.Level.WaterStyles</c> has
/// presets that reference only the game's shared textures.
/// </summary>
public sealed class WaterStyle
{
    /// <summary>The appearance fields. Don't set position, size or name fields here; the water objects set those.</summary>
    public SceneFields Fields { get; } = new();
}
