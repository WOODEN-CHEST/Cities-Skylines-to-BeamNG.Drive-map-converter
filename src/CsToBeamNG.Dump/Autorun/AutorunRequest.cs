using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsToBeamNG.Dump.Autorun;

/// <summary>
/// A request to the exporter to load a save, export it and, normally, quit the game, without anyone
/// clicking. A tool writes it to <see cref="AutorunFiles.RequestFileName"/> before it starts the game; the
/// exporter consumes it when the game loads the mod (docs/architecture.md, Unattended export).
/// </summary>
public sealed class AutorunRequest
{
    /// <summary>The format version this library writes and reads. Requests with another one are rejected.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// How old a request may be when the exporter reads it. An older one is left over from a tool that
    /// didn't clean up, and the user who starts the game now doesn't expect an export.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    private const string FormatKey = "format";
    private const string IdKey = "id";
    private const string CreatedKey = "created";
    private const string SaveKey = "save";
    private const string QuitKey = "quit";
    private const string ProbesKey = "probes";
    // Quoted so that no culture can change the separators.
    private const string TimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'";

    /// <summary>Creates a request.</summary>
    /// <param name="id">Identifies the request; the exporter copies it into its status. Must not be empty.</param>
    /// <param name="createdUtc">When the request was written, in UTC (a local time is converted). Stored to the second.</param>
    /// <param name="saveName">The save to load, as the game's Load panel lists it (normally the file name without <c>.crp</c>).</param>
    /// <param name="quitWhenDone">Whether the game should quit after the export, whether it succeeded or not.</param>
    /// <param name="probes">
    /// Developer probes to run with the export (<see cref="DumpProbes"/>), or <see langword="null"/> for none.
    /// Each name must pass <see cref="DumpProbes.IsValidName"/>.
    /// </param>
    public AutorunRequest(string id, DateTime createdUtc, string saveName, bool quitWhenDone, IList<string>? probes = null)
    {
        Guard.NotNull(id, nameof(id));
        Guard.NotNull(saveName, nameof(saveName));
        if (id.Length == 0)
        {
            throw new ArgumentException("The id must not be empty.", nameof(id));
        }

        if (saveName.Length == 0)
        {
            throw new ArgumentException("The save name must not be empty.", nameof(saveName));
        }

        var probeList = new List<string>();
        if (probes is not null)
        {
            foreach (string probe in probes)
            {
                if (!DumpProbes.IsValidName(probe))
                {
                    throw new ArgumentException("'" + probe + "' is not a valid probe name.", nameof(probes));
                }

                if (!probeList.Contains(probe))
                {
                    probeList.Add(probe);
                }
            }
        }

        Id = id;
        CreatedUtc = TruncateToSeconds(createdUtc);
        SaveName = saveName;
        QuitWhenDone = quitWhenDone;
        Probes = probeList.AsReadOnly();
    }

    /// <summary>Identifies the request; the exporter copies it into its status.</summary>
    public string Id { get; }

    /// <summary>When the request was written, in UTC, to the second.</summary>
    public DateTime CreatedUtc { get; }

    /// <summary>The save to load.</summary>
    public string SaveName { get; }

    /// <summary>Whether the game should quit after the export, whether it succeeded or not.</summary>
    public bool QuitWhenDone { get; }

    /// <summary>
    /// The developer probes to run with the export, without duplicates, in the order given. Empty for a
    /// normal export. The exporter ignores names it doesn't know, so a newer tool can ask for more.
    /// </summary>
    public IList<string> Probes { get; }

    /// <summary>
    /// Reads a request. Text that isn't a valid request of <see cref="FormatVersion"/> throws a
    /// <see cref="FormatException"/>; <see cref="TryReadId"/> may still find its id.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static AutorunRequest Parse(string text)
    {
        Dictionary<string, string> values = KeyValueText.Parse(text);
        AutorunFields.RequireFormat(values, FormatKey, FormatVersion, "request");
        string id = AutorunFields.Require(values, IdKey);
        string created = AutorunFields.Require(values, CreatedKey);
        if (!DateTime.TryParseExact(
            created,
            TimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime createdUtc))
        {
            throw new FormatException("'" + created + "' is not a time like 2026-11-17T16:30:00Z.");
        }

        string save = AutorunFields.Require(values, SaveKey);
        bool quit = AutorunFields.RequireBoolean(values, QuitKey);
        if (id.Length == 0 || save.Length == 0)
        {
            throw new FormatException("The id and the save must not be empty.");
        }

        var probes = new List<string>();
        string? probesText = AutorunFields.Optional(values, ProbesKey);
        if (probesText is not null)
        {
            foreach (string probe in probesText.Split(','))
            {
                if (!DumpProbes.IsValidName(probe))
                {
                    throw new FormatException("'" + probe + "' in '" + ProbesKey + "' is not a valid probe name.");
                }

                probes.Add(probe);
            }
        }

        return new AutorunRequest(id, createdUtc, save, quit, probes);
    }

    /// <summary>
    /// Finds the id in text that may not be a valid request, such as one of a newer format, so that the
    /// exporter can still report the problem under the right id. Returns <see langword="null"/> if there is none.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static string? TryReadId(string text)
    {
        try
        {
            return KeyValueText.Parse(text).TryGetValue(IdKey, out string? id) && id.Length > 0 ? id : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Whether the request is too old to act on (<see cref="MaxAge"/>) at the given time.</summary>
    /// <param name="nowUtc">The current time, in UTC.</param>
    public bool IsStale(DateTime nowUtc) => nowUtc - CreatedUtc > MaxAge;

    /// <summary>Returns the file's text. The probes key is left out when there are none.</summary>
    public string ToText()
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new(FormatKey, FormatVersion.ToString(CultureInfo.InvariantCulture)),
            new(IdKey, Id),
            new(CreatedKey, CreatedUtc.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            new(SaveKey, SaveName),
            new(QuitKey, AutorunFields.FormatBoolean(QuitWhenDone)),
        };
        if (Probes.Count > 0)
        {
            string[] names = new string[Probes.Count];
            Probes.CopyTo(names, 0);
            pairs.Add(new(ProbesKey, string.Join(",", names)));
        }

        return "# CsToBeamNG autorun request\n" + KeyValueText.Format(pairs);
    }

    private static DateTime TruncateToSeconds(DateTime time)
    {
        DateTime utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time;
        return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }
}
