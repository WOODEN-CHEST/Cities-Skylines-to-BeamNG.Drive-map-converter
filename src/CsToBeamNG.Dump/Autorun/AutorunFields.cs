using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsToBeamNG.Dump.Autorun;

/// <summary>Reading and writing of the fields that the autorun files share.</summary>
internal static class AutorunFields
{
    public static string Require(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) ? value : throw new FormatException("The key '" + key + "' is missing.");

    public static string? Optional(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) && value.Length > 0 ? value : null;

    public static bool RequireBoolean(Dictionary<string, string> values, string key) => Require(values, key) switch
    {
        "true" => true,
        "false" => false,
        string other => throw new FormatException("'" + key + "' must be true or false, not '" + other + "'."),
    };

    public static string FormatBoolean(bool value) => value ? "true" : "false";

    public static void RequireFormat(Dictionary<string, string> values, string key, int expected, string what)
    {
        string format = Require(values, key);
        if (format != expected.ToString(CultureInfo.InvariantCulture))
        {
            throw new FormatException(
                "The " + what + " has format " + format + "; this version of CsToBeamNG reads format "
                + expected.ToString(CultureInfo.InvariantCulture) + ". Use the exporter and the command-line tool from the same build.");
        }
    }
}
