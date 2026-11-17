using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Exporter.Core;
using CsToBeamNG.Exporter.Game;

namespace CsToBeamNG.Exporter.Export;

/// <summary>
/// One export. <see cref="Prepare"/> collects on the main thread what only it may read; <see cref="Run"/>
/// reads the city and writes the dump on the simulation thread.
/// </summary>
internal sealed class ExportJob
{
    private readonly CityReader _reader;
    private readonly string _dumpsFolder;
    private readonly DateTime _startUtc;
    private readonly DumpManifest _manifest;
    private readonly IList<string> _problems;
    private readonly string _gameVersionFull;
    private volatile bool _abandoned;

    private ExportJob(
        CityReader reader,
        string dumpsFolder,
        DateTime startUtc,
        DumpManifest manifest,
        IList<string> problems,
        string gameVersionFull)
    {
        _reader = reader;
        _dumpsFolder = dumpsFolder;
        _startUtc = startUtc;
        _manifest = manifest;
        _problems = problems;
        _gameVersionFull = gameVersionFull;
    }

    /// <summary>
    /// Prepares an export on the main thread: finds the game's managers and reads the versions and the
    /// enabled mods. Nothing is written yet.
    /// </summary>
    /// <param name="dumpsFolder">The folder for the new dump's folder.</param>
    public static ExportJob Prepare(string dumpsFolder)
    {
        DateTime startUtc = DateTime.UtcNow;
        CityReader reader = CityReader.Create();
        var problems = new List<string>();
        IList<GameModInfo> mods = ModListReader.ReadEnabledMods(problems);
        DumpManifest manifest = ManifestBuilder.CreateInitial(
            GetExporterVersion(),
            BuildConfig.applicationVersion,
            mods,
            reader.ReadCityName(),
            startUtc);
        return new ExportJob(reader, dumpsFolder, startUtc, manifest, problems, BuildConfig.applicationVersionFull);
    }

    /// <summary>Marks the export as abandoned because the city is being closed, so <see cref="Run"/> won't read it.</summary>
    public void Abandon() => _abandoned = true;

    /// <summary>
    /// Runs the export on the simulation thread, so the city can't change while it is read. It creates the
    /// dump folder, writes an incomplete manifest, reads the city, and completes the manifest. It never
    /// throws: failures are logged and returned.
    /// </summary>
    public ExportOutcome Run()
    {
        if (_abandoned)
        {
            const string Message = "The city was closed before the export could start. Nothing was exported.";
            GameLog.Warning(Message);
            return new ExportOutcome(false, Message);
        }

        DumpWriter? writer = null;
        ExportLog? log = null;
        try
        {
            string folder = DumpLocation.ChooseNewFolder(_dumpsFolder, _manifest.City.Name, _startUtc.ToLocalTime());
            writer = DumpWriter.Create(folder, _manifest);
            log = ExportLog.Create(writer.Folder, GameLog.Forward);
            log.Info("Exporting \"" + _manifest.City.Name + "\" to " + writer.Folder);
            LogVersions(log);
            foreach (string problem in _problems)
            {
                log.Warning(problem);
                _manifest.Warnings.Add(problem);
            }

            // Written now with complete: false, so that an export that is interrupted says so.
            writer.WriteManifest();

            CitySnapshot snapshot = _reader.Read();
            LogSnapshot(log, snapshot);
            foreach (string problem in ManifestBuilder.AddSnapshot(_manifest, snapshot))
            {
                log.Warning(problem);
            }

            _manifest.Complete = true;
            writer.WriteManifest();
            log.Info("Export finished: " + writer.Folder);
            return new ExportOutcome(true, SuccessMessage(writer.Folder, snapshot));
        }
        catch (Exception e)
        {
            return Fail(e, writer, log);
        }
        finally
        {
            log?.Dispose();
        }
    }

    private static string GetExporterVersion()
    {
        Assembly assembly = typeof(ExportJob).Assembly;
        object[] attributes = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
        return attributes.Length > 0 && attributes[0] is AssemblyInformationalVersionAttribute attribute
            && !string.IsNullOrEmpty(attribute.InformationalVersion)
            ? attribute.InformationalVersion
            : assembly.GetName().Version.ToString();
    }

    private static void LogSnapshot(ExportLog log, CitySnapshot snapshot)
    {
        log.Detail(string.Format(
            CultureInfo.InvariantCulture,
            "City: name \"{0}\", map \"{1}\", environment \"{2}\", map theme \"{3}\", left-hand traffic {4}",
            snapshot.CityName,
            snapshot.MapName,
            snapshot.Environment,
            snapshot.MapTheme,
            snapshot.LeftHandTraffic ? "yes" : "no"));
        log.Detail(snapshot.AreaGrid is null
            ? "Area grid: none"
            : "Area grid (" + snapshot.AreaGrid.Length.ToString(CultureInfo.InvariantCulture) + " entries, row by row): " + JoinNumbers(snapshot.AreaGrid));
        foreach (ObjectCount count in snapshot.Counts)
        {
            log.Detail(string.Format(
                CultureInfo.InvariantCulture,
                "Count {0}: {1} created, game counter {2}, buffer length {3}",
                count.Key,
                count.Created,
                count.GameCounter,
                count.Capacity));
            if (count.GameCounter != count.Created)
            {
                log.Detail("Count " + count.Key + ": the game's counter differs from the number of created objects.");
            }
        }
    }

    private static string JoinNumbers(int[] values)
    {
        var text = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                text.Append(' ');
            }

            text.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static long FindCount(CitySnapshot snapshot, string key)
    {
        foreach (ObjectCount count in snapshot.Counts)
        {
            if (count.Key == key)
            {
                return count.Created;
            }
        }

        return 0;
    }

    private void LogVersions(ExportLog log)
    {
        log.Detail("Exporter: " + _manifest.Exporter.Name + " " + _manifest.Exporter.Version);
        log.Detail("Game: " + _manifest.Game.Version + " (" + _gameVersionFull + ")");
        foreach (GameModInfo mod in _manifest.Game.Mods)
        {
            log.Detail("Enabled mod: " + mod.Name + (mod.WorkshopId is null ? " (local or built-in)" : " (workshop " + mod.WorkshopId + ")"));
        }
    }

    private string SuccessMessage(string folder, CitySnapshot snapshot)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            "Exported \"{0}\" to\n{1}\n\nThe city has {2} network nodes, {3} segments, {4} buildings, {5} props and {6} trees.",
            _manifest.City.Name,
            folder,
            FindCount(snapshot, CountKeys.NetNodes),
            FindCount(snapshot, CountKeys.NetSegments),
            FindCount(snapshot, CountKeys.Buildings),
            FindCount(snapshot, CountKeys.Props),
            FindCount(snapshot, CountKeys.Trees));
        return _manifest.Warnings.Count == 0
            ? message
            : message + "\n\nThere were warnings; see " + ExportLog.FileName + " in the dump folder.";
    }

    private ExportOutcome Fail(Exception exception, DumpWriter? writer, ExportLog? log)
    {
        string where;
        if (writer is null || log is null)
        {
            GameLog.Warning("The export failed: " + exception);
            where = "the game log";
        }
        else
        {
            log.Error("The export failed.", exception);
            where = Path.Combine(writer.Folder, ExportLog.FileName);
        }

        if (writer is not null)
        {
            try
            {
                _manifest.Complete = false;
                _manifest.Warnings.Add("The export failed: " + exception.Message);
                writer.WriteManifest();
            }
            catch (Exception e)
            {
                // The manifest from the start of the export, with complete: false, stays in place.
                log?.Error("The manifest couldn't be updated.", e);
            }
        }

        return new ExportOutcome(false, "The export failed: " + exception.Message + "\n\nDetails are in\n" + where);
    }
}
