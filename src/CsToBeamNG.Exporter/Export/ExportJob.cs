using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;
using CsToBeamNG.Exporter.Game;
using CsToBeamNG.Exporter.Probes;

namespace CsToBeamNG.Exporter.Export;

/// <summary>
/// One export. <see cref="Prepare"/> collects on the main thread what only it may read; <see cref="Run"/>
/// reads the city and writes the dump on the simulation thread.
/// </summary>
internal sealed class ExportJob
{
    private readonly CityReader _reader;
    private readonly TerrainReader? _terrain;
    private readonly string _dumpsFolder;
    private readonly DateTime _startUtc;
    private readonly DumpManifest _manifest;
    private readonly IList<string> _problems;
    private readonly string _gameVersionFull;
    private readonly IList<string> _probes;
    private volatile bool _abandoned;

    private ExportJob(
        CityReader reader,
        TerrainReader? terrain,
        string dumpsFolder,
        DateTime startUtc,
        DumpManifest manifest,
        IList<string> problems,
        string gameVersionFull,
        IList<string> probes)
    {
        _reader = reader;
        _terrain = terrain;
        _dumpsFolder = dumpsFolder;
        _startUtc = startUtc;
        _manifest = manifest;
        _problems = problems;
        _gameVersionFull = gameVersionFull;
        _probes = probes;
    }

    /// <summary>
    /// Prepares an export on the main thread: finds the game's managers and reads the versions and the
    /// enabled mods. Nothing is written yet.
    /// </summary>
    /// <param name="dumpsFolder">The folder for the new dump's folder.</param>
    /// <param name="probes">Developer probes to run after the city is read (<see cref="DumpProbes"/>).</param>
    public static ExportJob Prepare(string dumpsFolder, IList<string> probes)
    {
        DateTime startUtc = DateTime.UtcNow;
        CityReader reader = CityReader.Create();
        var problems = new List<string>();
        TerrainReader? terrain = CreateTerrainReader(problems);
        IList<GameModInfo> mods = ModListReader.ReadEnabledMods(problems);
        DumpManifest manifest = ManifestBuilder.CreateInitial(
            GetExporterVersion(),
            BuildConfig.applicationVersion,
            mods,
            reader.ReadCityName(),
            startUtc);
        return new ExportJob(reader, terrain, dumpsFolder, startUtc, manifest, problems, BuildConfig.applicationVersionFull, new List<string>(probes));
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
            return new ExportOutcome(false, Message, null);
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

            WriteSections(writer, log);
            string probeSummary = RunProbes(writer.Folder, log);
            _manifest.Complete = true;
            writer.WriteManifest();
            log.Info("Export finished: " + writer.Folder);
            return new ExportOutcome(true, SuccessMessage(writer.Folder, snapshot) + probeSummary, writer.Folder);
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

    private static TerrainReader? CreateTerrainReader(List<string> problems)
    {
        try
        {
            return TerrainReader.Create();
        }
        catch (InvalidOperationException e)
        {
            problems.Add("The terrain and the water can't be exported: " + e.Message);
            return null;
        }
    }

    // Each section is written on its own: one that fails is left out with a warning, and the export goes on
    // (agents/conventions.md, one bad object must not abort an export).
    private void WriteSections(DumpWriter writer, ExportLog log)
    {
        if (_terrain is null)
        {
            return;
        }

        WriteSection(log, TerrainSection.Name, () =>
        {
            var problems = new List<string>();
            TerrainSectionContent terrain = _terrain.ReadTerrain(problems);
            AddProblems(log, problems);
            TerrainSectionWriter.Write(writer, terrain);
            return SectionSummary.Describe(terrain);
        });
        WriteSection(log, WaterSection.Name, () =>
        {
            WaterSectionContent water = _terrain.ReadWater();
            WaterSectionWriter.Write(writer, water);
            return SectionSummary.Describe(water);
        });
    }

    private void WriteSection(ExportLog log, string name, Func<string> write)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            string summary = write();
            log.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Section {0} written in {1} ms: {2}",
                name,
                watch.ElapsedMilliseconds,
                summary));
        }
        catch (Exception e)
        {
            log.Warning("Section " + name + " couldn't be exported, so the dump has none: " + e);
            _manifest.Warnings.Add("The " + name + " couldn't be exported: " + e.Message);
        }
    }

    private void AddProblems(ExportLog log, List<string> problems)
    {
        foreach (string problem in problems)
        {
            log.Warning(problem);
            _manifest.Warnings.Add(problem);
        }
    }

    // Probes are developer tools: a failing probe is logged and reported, but the dump stays valid.
    private string RunProbes(string dumpFolder, ExportLog log)
    {
        var summary = new StringBuilder();
        foreach (string probe in _probes)
        {
            string folder = Path.Combine(Path.Combine(dumpFolder, DumpProbes.FolderName), probe);
            try
            {
                string result = probe == DumpProbes.Geometry
                    ? GeometryProbe.Write(folder, log)
                    : probe == DumpProbes.Heights
                        ? HeightsProbe.Write(folder, log)
                        : "skipped: this exporter doesn't know the probe";
                log.Info("Probe " + probe + ": " + result);
                summary.Append("\n\nProbe ").Append(probe).Append(": ").Append(result);
            }
            catch (Exception e)
            {
                log.Warning("Probe " + probe + " failed: " + e);
                summary.Append("\n\nProbe ").Append(probe).Append(" failed: ").Append(e.Message);
            }
        }

        return summary.ToString();
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

        return new ExportOutcome(false, "The export failed: " + exception.Message + "\n\nDetails are in\n" + where, writer?.Folder);
    }
}
