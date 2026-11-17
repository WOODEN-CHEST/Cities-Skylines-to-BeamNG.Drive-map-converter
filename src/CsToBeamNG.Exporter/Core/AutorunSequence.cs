using System.Globalization;
using CsToBeamNG.Dump.Autorun;

namespace CsToBeamNG.Exporter.Core;

/// <summary>
/// Carries out an autorun request: wait for the main menu, load the save, export it, report the result
/// and, if asked, quit. The game calls <see cref="Update"/> every frame; everything runs on the main
/// thread (docs/architecture.md, Unattended export).
/// </summary>
/// <remarks>
/// It has no timeouts of its own while loading and exporting, which can take long for big cities: the
/// tool that made the request decides how long to wait.
/// </remarks>
internal sealed class AutorunSequence
{
    /// <summary>
    /// How long the main menu must have been idle before the save is loaded. The game's own automatic
    /// Continue (the <c>-continuelastsave</c> option) waits 2.5 s after showing the menu
    /// (docs/research/cs1-save-and-world.md, Loading a save from a mod).
    /// </summary>
    public const double MenuSettleSeconds = 2.5;

    /// <summary>
    /// How long the main menu must be idle again, while the save is supposed to be loading, before the
    /// load counts as failed. The game goes back to the menu when a save can't be loaded.
    /// </summary>
    public const double BackInMenuSeconds = 10;

    /// <summary>How long the game may take to quit before it is forced to.</summary>
    public const double QuitTimeoutSeconds = 60;

    private readonly AutorunRequest _request;
    private readonly IAutorunHost _host;
    private double _now;
    private double? _menuIdleSince;
    private double _quitRequestedAt;

    /// <summary>Creates the sequence for a request. Nothing happens before <see cref="Start"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="host">The game.</param>
    public AutorunSequence(AutorunRequest request, IAutorunHost host)
    {
        _request = request;
        _host = host;
    }

    /// <summary>Where the sequence is.</summary>
    public AutorunPhase Phase { get; private set; } = AutorunPhase.WaitingForMenu;

    /// <summary>Reports that the request was accepted.</summary>
    /// <param name="nowSeconds">The current time, from a clock that keeps running while the game is paused.</param>
    public void Start(double nowSeconds)
    {
        _now = nowSeconds;
        _host.Log("Autorun: accepted request " + _request.Id + " to export the save \"" + _request.SaveName + "\"" + (_request.QuitWhenDone ? " and quit." : "."));
        Report(AutorunState.Accepted, "Waiting for the main menu.", null);
    }

    /// <summary>Advances the sequence. Called every frame.</summary>
    /// <param name="nowSeconds">The current time, from a clock that keeps running while the game is paused.</param>
    public void Update(double nowSeconds)
    {
        _now = nowSeconds;
        switch (Phase)
        {
            case AutorunPhase.WaitingForMenu:
                if (HasMenuBeenIdleFor(MenuSettleSeconds))
                {
                    LoadSave();
                }

                break;
            case AutorunPhase.LoadingSave:
                if (_host.IsCityLoaded)
                {
                    Phase = AutorunPhase.Exporting;
                    _host.Log("Autorun: the city is loaded; exporting.");
                    Report(AutorunState.Exporting, "Exporting the city.", null);
                    _host.StartExport();
                }
                else if (HasMenuBeenIdleFor(BackInMenuSeconds))
                {
                    Finish(AutorunState.Failed, "The game went back to the main menu instead of loading the save \"" + _request.SaveName + "\". The game log may say why.", null);
                }

                break;
            case AutorunPhase.Quitting:
                if (_now - _quitRequestedAt >= QuitTimeoutSeconds)
                {
                    _host.Warn("Autorun: the game didn't quit within " + QuitTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " s; forcing it.");
                    Phase = AutorunPhase.Done;
                    _host.ForceQuit();
                }

                break;
            case AutorunPhase.Exporting:
            case AutorunPhase.Done:
            default:
                break;
        }
    }

    /// <summary>Called on the main thread when the export started by the host has ended.</summary>
    /// <param name="succeeded">Whether the dump is complete.</param>
    /// <param name="message">The export's message for the user.</param>
    /// <param name="dumpFolder">The dump's folder, if one was created.</param>
    public void OnExportFinished(bool succeeded, string message, string? dumpFolder)
    {
        if (Phase != AutorunPhase.Exporting)
        {
            _host.Warn("Autorun: an export ended while the sequence was in phase " + Phase + "; ignored.");
            return;
        }

        Finish(succeeded ? AutorunState.Succeeded : AutorunState.Failed, message, dumpFolder);
    }

    /// <summary>
    /// Ends the sequence with a failure, for problems the host catches, such as an exception. When the
    /// final status is already out, only makes sure the game quits if it should.
    /// </summary>
    /// <param name="message">What went wrong, for the user.</param>
    public void Fail(string message)
    {
        switch (Phase)
        {
            case AutorunPhase.Quitting:
                Phase = AutorunPhase.Done;
                _host.ForceQuit();
                break;
            case AutorunPhase.Done:
                break;
            default:
                Finish(AutorunState.Failed, message, null);
                break;
        }
    }

    private void LoadSave()
    {
        _host.Log("Autorun: the main menu is ready; loading the save \"" + _request.SaveName + "\".");
        string? problem = _host.StartLoadingSave(_request.SaveName);
        if (problem is not null)
        {
            Finish(AutorunState.Failed, problem, null);
            return;
        }

        Phase = AutorunPhase.LoadingSave;
        _menuIdleSince = null;
        Report(AutorunState.LoadingSave, "Loading the save \"" + _request.SaveName + "\".", null);
    }

    private bool HasMenuBeenIdleFor(double seconds)
    {
        if (!_host.IsMenuIdle)
        {
            _menuIdleSince = null;
            return false;
        }

        _menuIdleSince ??= _now;
        return _now - _menuIdleSince.Value >= seconds;
    }

    private void Finish(AutorunState state, string message, string? dumpFolder)
    {
        if (state == AutorunState.Succeeded)
        {
            _host.Log("Autorun: finished: " + message.Replace('\n', ' '));
        }
        else
        {
            _host.Warn("Autorun: failed: " + message.Replace('\n', ' '));
        }

        Report(state, message, dumpFolder);
        if (_request.QuitWhenDone)
        {
            _host.Log("Autorun: quitting the game.");
            Phase = AutorunPhase.Quitting;
            _quitRequestedAt = _now;
            _host.Quit();
        }
        else
        {
            Phase = AutorunPhase.Done;
        }
    }

    private void Report(AutorunState state, string message, string? dumpFolder) =>
        _host.ReportStatus(new AutorunStatus(_request.Id, state, message, dumpFolder));
}
