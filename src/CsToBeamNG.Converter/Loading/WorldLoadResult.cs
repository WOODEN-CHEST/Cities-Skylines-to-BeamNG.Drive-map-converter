using System.Collections.Generic;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Loading;

/// <summary>The world model built from a dump, and what happened to each section on the way.</summary>
public sealed class WorldLoadResult
{
    internal WorldLoadResult(
        CityWorld world,
        IReadOnlyList<SectionLoadResult> sections,
        IReadOnlyList<string> missingSections,
        IReadOnlyList<LoadMessage> messages)
    {
        World = world;
        Sections = sections;
        MissingSections = missingSections;
        Messages = messages;
    }

    /// <summary>The world model.</summary>
    public CityWorld World { get; }

    /// <summary>Every section of the dump, in manifest order, with what happened to it.</summary>
    public IReadOnlyList<SectionLoadResult> Sections { get; }

    /// <summary>The sections that a registered loader reads but the dump doesn't have, in loader order. That is allowed.</summary>
    public IReadOnlyList<string> MissingSections { get; }

    /// <summary>Everything reported while loading: first about the dump as a whole, then per section in manifest order.</summary>
    public IReadOnlyList<LoadMessage> Messages { get; }

    /// <summary>Whether a message is an error: data that should be there couldn't be read.</summary>
    public bool HasErrors
    {
        get
        {
            foreach (LoadMessage message in Messages)
            {
                if (message.Severity == LoadMessageSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
