namespace CsToBeamNG.Converter.Discovery;

/// <summary>A location given by the user instead of being searched for.</summary>
/// <param name="Path">The path as the user gave it. It may be relative or start with <c>~</c>.</param>
/// <param name="Origin">Where it came from, such as <c>--cs1-dir</c> or <c>CSTOBEAMNG_CS1_DIR</c>.</param>
public sealed record PathOverride(string Path, string Origin);
