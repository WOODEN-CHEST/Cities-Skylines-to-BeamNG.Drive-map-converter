using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>Reads the real Windows registry.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsRegistryReader : IRegistryReader
{
    public string? GetString(RegistryRoot root, string keyPath, string valueName)
    {
        RegistryKey hive = root == RegistryRoot.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
        try
        {
            using RegistryKey? key = hive.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // An unreadable key counts as missing; the locator then tries its next candidate.
            return null;
        }
    }
}
