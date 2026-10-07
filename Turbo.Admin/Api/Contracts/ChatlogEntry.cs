using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One line of room chat: when, who said it, in which room, and for a whisper who it was to.
/// Names are null when the player or room is gone.
/// </summary>
public sealed record ChatlogEntry(
    int Id,
    DateTime AtUtc,
    int PlayerId,
    string? PlayerName,
    int RoomId,
    string? RoomName,
    int? TargetPlayerId,
    string? TargetPlayerName,
    string Message
);
