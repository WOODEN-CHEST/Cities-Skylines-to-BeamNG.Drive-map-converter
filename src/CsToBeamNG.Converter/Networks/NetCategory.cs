namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// What kind of network a prefab is, for drawing and summaries: the classes of the road map, plus everything else
/// (<see cref="NetCategories.Of"/>; docs/research/cs1-networks.md, Prefabs).
/// </summary>
public enum NetCategory
{
    /// <summary>Not drawn: pipes, power lines, walls, transport lines, ship and air routes, and networks of unknown prefabs.</summary>
    Other,

    /// <summary>Paths for pedestrians and bicycles.</summary>
    Path,

    /// <summary>Tracks for trains, metros, trams, monorails and cable cars.</summary>
    Rail,

    /// <summary>Small roads: roads of item-class levels 1 and 2, such as the two-lane roads.</summary>
    Local,

    /// <summary>Medium and large roads: roads of item-class level 3 and up without highway rules.</summary>
    Arterial,

    /// <summary>Roads with highway rules: highways and their ramps.</summary>
    Highway,
}
