namespace CsToBeamNG.Converter.Networks;

/// <summary>Which way the traffic on a segment goes, if only one way (<see cref="NetDirections.OneWayOf"/>).</summary>
public enum NetOneWay
{
    /// <summary>Both ways, or the segment has no traffic lanes.</summary>
    None,

    /// <summary>Only from the start node towards the end node.</summary>
    StartToEnd,

    /// <summary>Only from the end node towards the start node.</summary>
    EndToStart,
}
