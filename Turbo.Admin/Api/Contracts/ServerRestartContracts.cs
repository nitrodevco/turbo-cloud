using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A restart from the panel: the countdown in whole minutes, and the reason shown to players.</summary>
public sealed record ServerRestartRequest(int? Minutes, string? Message);

/// <summary>
/// Whether the signed-in staff member may restart the server, and the restart under way if one
/// is: counting down (<c>Scheduled</c>, until <c>AtUtc</c>) or stopping (<c>Restarting</c>), the
/// reason players were given, and the exit code the process ends with for the host to start it
/// again. <c>MaxMinutes</c> is the longest countdown allowed.
/// </summary>
public sealed record ServerRestartStatus(
    bool CanRestart,
    bool Scheduled,
    bool Restarting,
    DateTime? AtUtc,
    string Reason,
    int ExitCode,
    int MaxMinutes
);
