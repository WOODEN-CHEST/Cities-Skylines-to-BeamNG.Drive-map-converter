namespace CsToBeamNG.Converter.Terrain;

/// <summary>How <see cref="CityTerrain.SampleGround(double, double, CoarseInterpolation)"/> reads the 16 m final heights.</summary>
public enum CoarseInterpolation
{
    /// <summary>Bilinearly, as the game draws them (<c>SampleDetailHeight</c>, <c>SampleFinalHeight</c>).</summary>
    Bilinear,

    /// <summary>
    /// With <see cref="MonotoneCubic"/>: the same heights at the grid points, but without the 16 m grid's facets
    /// between them. The 4 m detail heights are still read bilinearly, as the game does.
    /// </summary>
    MonotoneCubic,
}
