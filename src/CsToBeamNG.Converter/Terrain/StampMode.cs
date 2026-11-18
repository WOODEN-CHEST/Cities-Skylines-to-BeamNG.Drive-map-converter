namespace CsToBeamNG.Converter.Terrain;

/// <summary>How a <see cref="FootprintStamp"/> combines its height with the terrain's.</summary>
public enum StampMode
{
    /// <summary>The terrain takes the footprint's height, such as a level building lot.</summary>
    Set,

    /// <summary>The terrain is lowered to the footprint's height where it is higher, such as under a road surface.</summary>
    Lower,

    /// <summary>The terrain is raised to the footprint's height where it is lower, such as an embankment.</summary>
    Raise,
}
