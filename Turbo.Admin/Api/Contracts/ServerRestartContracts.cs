using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A restart from the panel: the countdown in whole minutes, the reason shown to players, and
/// where its countdown notice links to (a web address or a client link; empty, the hotel's default).
/// </summary>
public sealed record ServerRestartRequest(int? Minutes, string? Message, string? Link = null);

/// <summary>
/// Whether the signed-in staff member may restart the server, and the restart under way if one
/// is: counting down (<c>Scheduled</c>, until <c>AtUtc</c>) or stopping (<c>Restarting</c>), the
/// reason players were given, and the exit code the process ends with for the host to start it
/// again. <c>MaxMinutes</c> is the longest countdown allowed. <c>Notice</c> is whether players
/// are shown a countdown notice for a restart (<c>Turbo:Operations:RestartNotice</c>), and
/// <c>NoticeLink</c> where the one under way links to, or the hotel's default when none is.
/// </summary>
public sealed record ServerRestartStatus(
    bool CanRestart,
    bool Scheduled,
    bool Restarting,
    DateTime? AtUtc,
    string Reason,
    int ExitCode,
    int MaxMinutes,
    bool Notice,
    string NoticeLink
);
