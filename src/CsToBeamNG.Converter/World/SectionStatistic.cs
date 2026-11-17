namespace CsToBeamNG.Converter.World;

/// <summary>A figure about a section's data for <c>cstobeamng inspect</c>, such as <c>segments: 2482</c>.</summary>
/// <param name="Name">What it measures, in lowercase words, such as <c>segments</c> or <c>height range</c>.</param>
/// <param name="Value">The value, formatted with the invariant culture and with its unit, such as <c>12.5 to 410 m</c>.</param>
public sealed record SectionStatistic(string Name, string Value);
