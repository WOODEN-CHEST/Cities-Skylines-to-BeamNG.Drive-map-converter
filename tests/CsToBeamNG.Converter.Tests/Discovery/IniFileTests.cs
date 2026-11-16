using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Discovery;

public sealed class IniFileTests
{
    [Fact]
    public void ReadsTheWindowsVersionFileOfBeamNG()
    {
        // As written by BeamNG.drive on Windows (seen in a Proton prefix): byte order mark, CRLF.
        const string Text = "\uFEFFversion = 0.38.3.0\r\ninstallPath = Z:\\games\\BeamNG.drive\\\r\n";

        Assert.Equal("0.38.3.0", IniFile.ReadValue(Text, "version"));
        Assert.Equal(@"Z:\games\BeamNG.drive\", IniFile.ReadValue(Text, "INSTALLPATH"));
    }

    [Fact]
    public void SkipsCommentsAndReturnsEmptyValues()
    {
        const string Text = "; UserPath = D:\\commented\n# also a comment\n[section]\n;UserPath = .\\\nUserPath =\n";

        Assert.Equal(string.Empty, IniFile.ReadValue(Text, "UserPath"));
        Assert.Null(IniFile.ReadValue(Text, "Missing"));
    }
}
