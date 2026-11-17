using System;
using System.Collections.Generic;
using System.Text;

namespace CsToBeamNG.Exporter.Core;

/// <summary>Finds the save an autorun request names among the saves the game knows.</summary>
internal static class SaveMatcher
{
    private const int MaxListedNames = 20;

    /// <summary>
    /// Returns the index of the save named <paramref name="requested"/>, or -1. A save matches when its
    /// listed name or its file name equals the request exactly; failing that, ignoring case. When several
    /// match equally well, such as a local save and a Steam Cloud save of the same name, the newest wins.
    /// </summary>
    /// <param name="saves">The saves.</param>
    /// <param name="requested">The requested name.</param>
    public static int FindBest(IList<SaveCandidate> saves, string requested)
    {
        int best = FindNewest(saves, requested, StringComparison.Ordinal);
        return best >= 0 ? best : FindNewest(saves, requested, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The message for a save that wasn't found, with the names of the saves that exist.</summary>
    /// <param name="saves">The saves.</param>
    /// <param name="requested">The requested name.</param>
    public static string DescribeNotFound(IList<SaveCandidate> saves, string requested)
    {
        if (saves.Count == 0)
        {
            return "The game has no saves, so \"" + requested + "\" can't be loaded.";
        }

        var names = new List<string>();
        foreach (SaveCandidate save in saves)
        {
            if (!names.Contains(save.Name))
            {
                names.Add(save.Name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        var message = new StringBuilder("The game has no save named \"" + requested + "\". Its saves are: ");
        for (int i = 0; i < names.Count && i < MaxListedNames; i++)
        {
            message.Append(i == 0 ? "\"" : ", \"").Append(names[i]).Append('"');
        }

        message.Append(names.Count > MaxListedNames ? ", ..." : ".");
        return message.ToString();
    }

    private static int FindNewest(IList<SaveCandidate> saves, string requested, StringComparison comparison)
    {
        int best = -1;
        for (int i = 0; i < saves.Count; i++)
        {
            SaveCandidate save = saves[i];
            bool matches = string.Equals(save.Name, requested, comparison)
                || (save.FileName is not null && string.Equals(save.FileName, requested, comparison));
            if (matches && (best < 0 || save.SavedTime > saves[best].SavedTime))
            {
                best = i;
            }
        }

        return best;
    }
}
