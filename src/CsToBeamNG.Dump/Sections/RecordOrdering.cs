using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Puts a section's objects in the order its specification requires: by id or index, each once.</summary>
internal static class RecordOrdering
{
    /// <summary>Returns a copy of the objects sorted by their key, after checking that none is missing or listed twice.</summary>
    /// <remarks>
    /// The exporter reads objects in id order, so the copy is usually in order already; one pass checks that and skips
    /// the sort, which matters for the hundreds of thousands of trees on the game's old runtime.
    /// </remarks>
    /// <param name="items">The objects.</param>
    /// <param name="key">Their id or index.</param>
    /// <param name="kind">What an object is, capitalised, for messages, such as <c>Building</c>.</param>
    /// <param name="parameterName">The parameter that holds the objects, for exceptions.</param>
    /// <exception cref="ArgumentException">An object is missing or its key is listed twice.</exception>
    public static List<T> SortUnique<T>(IList<T> items, Func<T, uint> key, string kind, string parameterName)
        where T : class
    {
        var sorted = new List<T>(items);
        bool strictlyIncreasing = true;
        uint previous = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            Guard.NotNull(sorted[i], parameterName);
            uint current = key(sorted[i]);
            strictlyIncreasing &= i == 0 || current > previous;
            previous = current;
        }

        if (strictlyIncreasing)
        {
            return sorted;
        }

        sorted.Sort((a, b) => key(a).CompareTo(key(b)));
        for (int i = 1; i < sorted.Count; i++)
        {
            if (key(sorted[i]) == key(sorted[i - 1]))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "{0} {1} is listed twice.", kind, key(sorted[i])),
                    parameterName);
            }
        }

        return sorted;
    }
}
