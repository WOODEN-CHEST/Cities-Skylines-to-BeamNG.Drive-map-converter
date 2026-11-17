namespace CsToBeamNG.Dump.Manifest;

/// <summary>The program that wrote a dump.</summary>
public sealed class ExporterInfo
{
    /// <summary>Creates an empty instance, to fill in.</summary>
    public ExporterInfo()
    {
    }

    /// <summary>Creates an instance.</summary>
    /// <param name="name">The program's name.</param>
    /// <param name="version">The program's version.</param>
    public ExporterInfo(string name, string version)
    {
        Name = name;
        Version = version;
    }

    /// <summary>The program's name, such as <c>CsToBeamNG.Exporter</c>. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The program's version, in any form. Required.</summary>
    public string Version { get; set; } = string.Empty;
}
