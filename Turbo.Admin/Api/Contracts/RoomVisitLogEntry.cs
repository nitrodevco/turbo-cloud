using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One line of the room visits log: a player going into a room, and when. The names are null for
/// a player or room that is gone.
/// </summary>
public sealed record RoomVisitLogEntry(
    int Id,
    DateTime AtUtc,
    int PlayerId,
    string? PlayerName,
    int RoomId,
    string? RoomName
);
