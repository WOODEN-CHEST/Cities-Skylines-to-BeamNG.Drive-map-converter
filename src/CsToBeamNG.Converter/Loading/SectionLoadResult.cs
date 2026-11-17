using System.Collections.Generic;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;

namespace CsToBeamNG.Converter.Loading;

/// <summary>What happened to one section of a dump during loading.</summary>
/// <param name="Section">The section as the dump lists it: name, version, files.</param>
/// <param name="Status">Whether it was loaded or why it was skipped.</param>
/// <param name="LoaderVersion">The version its loader was written for, or <see langword="null"/> if no loader knows it.</param>
/// <param name="Data">The loaded data, or <see langword="null"/> if it was skipped.</param>
/// <param name="Messages">What was reported about the section, in order.</param>
public sealed record SectionLoadResult(
    DumpSection Section,
    SectionLoadStatus Status,
    DumpVersion? LoaderVersion,
    ISectionData? Data,
    IReadOnlyList<LoadMessage> Messages);
