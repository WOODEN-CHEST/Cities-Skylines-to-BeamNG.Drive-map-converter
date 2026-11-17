using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Converter.Automation;

/// <summary>
/// Starts Cities: Skylines directly, without Steam's launch configuration, which would open the Paradox
/// launcher and wait for a click (docs/research/cs1-save-and-world.md, Starting the game without the
/// launcher).
/// </summary>
/// <remarks>
/// The game calls Steam's <c>SteamAPI_RestartAppIfNecessary</c> at startup, which restarts it through Steam
/// unless it was started by Steam. Steam marks the processes it starts with the <c>SteamAppId</c>
/// environment variable, so the launcher sets it, and the game then runs as if Steam had started it. The
/// Steam client must be running.
/// </remarks>
public sealed class CitiesSkylinesLauncher : IGameLauncher
{
    private const string SteamProcessName = "steam";

    private readonly string _installDir;
    private readonly OsKind _os;

    /// <summary>Creates a launcher.</summary>
    /// <param name="installDir">The game's install folder.</param>
    /// <param name="os">The OS, which decides the executable.</param>
    public CitiesSkylinesLauncher(string installDir, OsKind os)
    {
        ArgumentNullException.ThrowIfNull(installDir);
        _installDir = installDir;
        _os = os;
    }

    /// <summary>The native executable in the install folder: <c>Cities.exe</c> on Windows, <c>Cities.x64</c> on Linux.</summary>
    /// <param name="os">The OS.</param>
    public static string GetExecutableName(OsKind os) => os == OsKind.Windows ? "Cities.exe" : "Cities.x64";

    /// <summary>The game's process name, as <see cref="Process.GetProcessesByName(string)"/> takes it.</summary>
    /// <param name="os">The OS.</param>
    public static string GetProcessName(OsKind os) => os == OsKind.Windows ? "Cities" : "Cities.x64";

    /// <summary>The executable's full path.</summary>
    public string ExecutablePath => Path.Join(_installDir, GetExecutableName(_os));

    /// <summary>How the game is started: the executable in its folder, with <c>SteamAppId</c> set.</summary>
    public ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            WorkingDirectory = _installDir,
            UseShellExecute = false,

            // The game's own log goes to its log file; what it prints is noise here.
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["SteamAppId"] = CitiesSkylinesLocator.AppId;
        startInfo.Environment["SteamGameId"] = CitiesSkylinesLocator.AppId;
        return startInfo;
    }

    /// <inheritdoc/>
    public bool IsGameRunning() => IsRunning(GetProcessName(_os));

    /// <inheritdoc/>
    public bool IsSteamRunning() => IsRunning(SteamProcessName);

    /// <inheritdoc/>
    public IGameProcess Start()
    {
        if (!File.Exists(ExecutablePath))
        {
            throw new FileNotFoundException($"The game's executable '{ExecutablePath}' doesn't exist.", ExecutablePath);
        }

        try
        {
            Process process = Process.Start(CreateStartInfo())
                ?? throw new InvalidOperationException($"'{ExecutablePath}' didn't start.");

            // Read the redirected output so a full pipe can't block the game.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new GameProcess(process);
        }
        catch (Win32Exception e)
        {
            throw new InvalidOperationException($"'{ExecutablePath}' couldn't be started: {e.Message}", e);
        }
    }

    private static bool IsRunning(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        foreach (Process process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    private sealed class GameProcess(Process process) : IGameProcess
    {
        public int Id => process.Id;

        public bool HasExited => process.HasExited;

        public int? ExitCode => process.HasExited ? process.ExitCode : null;

        public void Kill()
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It has already exited.
            }
        }

        public void Dispose() => process.Dispose();
    }
}
