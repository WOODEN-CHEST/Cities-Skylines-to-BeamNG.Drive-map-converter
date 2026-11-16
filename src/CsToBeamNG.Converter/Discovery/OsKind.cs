namespace CsToBeamNG.Converter.Discovery;

/// <summary>The operating system whose conventions the locator applies.</summary>
public enum OsKind
{
    /// <summary>Windows: the registry, <c>%LOCALAPPDATA%</c> and Program Files.</summary>
    Windows,

    /// <summary>Linux: the home folder, XDG folders, symlinked Steam roots and Proton prefixes.</summary>
    Linux,
}
