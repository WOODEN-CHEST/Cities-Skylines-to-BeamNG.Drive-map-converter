namespace CsToBeamNG.Converter.Buildings;

/// <summary>
/// What kind of building a prefab is, for drawing and summaries: the four zones the player paints, and everything else
/// (<see cref="BuildingCategories.Of"/>).
/// </summary>
public enum BuildingCategory
{
    /// <summary>Not zoned: services, parks, monuments, transport, player-built industries, map decorations, and unknown prefabs.</summary>
    Other,

    /// <summary>Buildings that grow in residential zones.</summary>
    Residential,

    /// <summary>Buildings that grow in commercial zones.</summary>
    Commercial,

    /// <summary>Buildings that grow in industrial zones, including the specialised industries.</summary>
    Industrial,

    /// <summary>Buildings that grow in office zones.</summary>
    Office,
}
