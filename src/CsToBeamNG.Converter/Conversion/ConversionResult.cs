using System.Collections.Generic;
using CsToBeamNG.Converter.BeamNG;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>A converted level: its files, ready for <c>ModInstaller</c>, and the report about it.</summary>
/// <param name="LevelName">The level's name, which is also its mod folder's name.</param>
/// <param name="Files">The level's files, at their virtual paths; the report is one of them.</param>
/// <param name="Report">The report.</param>
public sealed record ConversionResult(string LevelName, IReadOnlyList<OutputFile> Files, ConversionReport Report);
