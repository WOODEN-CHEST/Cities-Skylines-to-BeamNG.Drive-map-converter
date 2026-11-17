using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsToBeamNG.Dump.Autorun;

/// <summary>
/// What the exporter reports about an autorun request. It rewrites <see cref="AutorunFiles.StatusFileName"/>
/// whenever the state changes; the tool that wrote the request reads it (docs/architecture.md, Unattended
/// export).
/// </summary>
public sealed class AutorunStatus
{
    /// <summary>The format version this library writes and reads.</summary>
    public const int FormatVersion = 1;

    private const string FormatKey = "format";
    private const string RequestIdKey = "request-id";
    private const string StateKey = "state";
    private const string MessageKey = "message";
    private const string DumpKey = "dump";

    private static readonly string[] _stateNames = ["accepted", "loading-save", "exporting", "succeeded", "failed"];

    /// <summary>Creates a status.</summary>
    /// <param name="requestId">The request's id, or <see langword="null"/> if the request was unreadable and had none.</param>
    /// <param name="state">The state.</param>
    /// <param name="message">A message for the user, if any; for <see cref="AutorunState.Failed"/>, why.</param>
    /// <param name="dumpFolder">The dump's folder, once there is one.</param>
    public AutorunStatus(string? requestId, AutorunState state, string? message, string? dumpFolder)
    {
        if ((int)state < 0 || (int)state >= _stateNames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        RequestId = string.IsNullOrEmpty(requestId) ? null : requestId;
        State = state;
        Message = string.IsNullOrEmpty(message) ? null : message;
        DumpFolder = string.IsNullOrEmpty(dumpFolder) ? null : dumpFolder;
    }

    /// <summary>The request's id, or <see langword="null"/> if the request was unreadable and had none.</summary>
    public string? RequestId { get; }

    /// <summary>The state.</summary>
    public AutorunState State { get; }

    /// <summary>A message for the user, if any; for <see cref="AutorunState.Failed"/>, why.</summary>
    public string? Message { get; }

    /// <summary>The dump's folder, once there is one.</summary>
    public string? DumpFolder { get; }

    /// <summary>Whether the exporter is done with the request.</summary>
    public bool IsFinal => State is AutorunState.Succeeded or AutorunState.Failed;

    /// <summary>The name of a state in the file, such as <c>loading-save</c>.</summary>
    /// <param name="state">The state.</param>
    public static string GetStateName(AutorunState state) => _stateNames[(int)state];

    /// <summary>Reads a status. Invalid text throws a <see cref="FormatException"/>.</summary>
    /// <param name="text">The file's text.</param>
    public static AutorunStatus Parse(string text)
    {
        Dictionary<string, string> values = KeyValueText.Parse(text);
        AutorunFields.RequireFormat(values, FormatKey, FormatVersion, "status");
        string stateName = AutorunFields.Require(values, StateKey);
        int state = Array.IndexOf(_stateNames, stateName);
        if (state < 0)
        {
            throw new FormatException("'" + stateName + "' is not a known state.");
        }

        return new AutorunStatus(
            AutorunFields.Optional(values, RequestIdKey),
            (AutorunState)state,
            AutorunFields.Optional(values, MessageKey),
            AutorunFields.Optional(values, DumpKey));
    }

    /// <summary>Returns the file's text.</summary>
    public string ToText() => "# CsToBeamNG autorun status\n" + KeyValueText.Format(
    [
        new(FormatKey, FormatVersion.ToString(CultureInfo.InvariantCulture)),
        new(RequestIdKey, RequestId ?? string.Empty),
        new(StateKey, GetStateName(State)),
        new(MessageKey, Message ?? string.Empty),
        new(DumpKey, DumpFolder ?? string.Empty),
    ]);
}
