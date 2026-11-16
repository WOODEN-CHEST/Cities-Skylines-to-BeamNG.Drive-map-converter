using System;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Everything the locator needs from the machine, passed in explicitly so that tests can simulate
/// Windows and Linux layouts on either OS. Every location the locator probes is derived from these
/// values or from files it reads; no absolute path is built in.
/// </summary>
public sealed record DiscoveryContext
{
    /// <summary>The OS whose conventions apply.</summary>
    public required OsKind Os { get; init; }

    /// <summary>The user's home folder (<c>$HOME</c>, or <c>%USERPROFILE%</c> on Windows).</summary>
    public required string HomeDir { get; init; }

    /// <summary>The folder that relative override paths are resolved against.</summary>
    public required string WorkingDir { get; init; }

    /// <summary><c>%LOCALAPPDATA%</c> (Windows only).</summary>
    public string? LocalAppData { get; init; }

    /// <summary><c>%ProgramFiles(x86)%</c> (Windows only): the parent of Steam's default folder.</summary>
    public string? ProgramFilesX86 { get; init; }

    /// <summary><c>$XDG_DATA_HOME</c> (Linux only). Empty means the default, <c>~/.local/share</c>.</summary>
    public string? XdgDataHome { get; init; }

    /// <summary><c>$XDG_CONFIG_HOME</c> (Linux only). Empty means the default, <c>~/.config</c>.</summary>
    public string? XdgConfigHome { get; init; }

    /// <summary>Access to the Windows registry, or <see langword="null"/> to skip registry lookups.</summary>
    public IRegistryReader? Registry { get; init; }

    /// <summary>Locations given by the user.</summary>
    public DiscoveryOverrides Overrides { get; init; } = new();

    /// <summary>
    /// Creates the context of the running process: its OS, environment variables and registry, and
    /// the overrides from the <c>CSTOBEAMNG_*</c> environment variables.
    /// </summary>
    /// <param name="commandLineOverrides">Overrides that take precedence over the environment variables.</param>
    /// <returns>The context.</returns>
    public static DiscoveryContext ForCurrentProcess(DiscoveryOverrides commandLineOverrides)
    {
        ArgumentNullException.ThrowIfNull(commandLineOverrides);

        bool isWindows = OperatingSystem.IsWindows();
        return new DiscoveryContext
        {
            Os = isWindows ? OsKind.Windows : OsKind.Linux,
            HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            WorkingDir = Environment.CurrentDirectory,
            LocalAppData = isWindows ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : null,
            ProgramFilesX86 = isWindows ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) : null,
            XdgDataHome = isWindows ? null : Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            XdgConfigHome = isWindows ? null : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            Registry = OperatingSystem.IsWindows() ? new WindowsRegistryReader() : null,
            Overrides = DiscoveryOverrides.FromEnvironment(Environment.GetEnvironmentVariable).OverriddenBy(commandLineOverrides),
        };
    }
}
