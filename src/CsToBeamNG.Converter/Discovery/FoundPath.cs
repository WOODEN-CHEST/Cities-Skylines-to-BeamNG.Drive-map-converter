namespace CsToBeamNG.Converter.Discovery;

/// <summary>A located file or folder.</summary>
/// <param name="Path">The full path.</param>
/// <param name="Source">How the path was determined.</param>
/// <param name="Exists">
/// Whether it exists. A location that was expected but not found is reported with the path where it
/// was expected and <see langword="false"/>.
/// </param>
/// <param name="Detail">
/// More about the source: the origin of an override, the candidate a Steam root was found at, or the
/// Steam library.
/// </param>
public sealed record FoundPath(string Path, LocationSource Source, bool Exists, string? Detail = null);
