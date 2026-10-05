using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One logged command: when, who ran it (0 for the console), in which room (0 for none), what
/// with, how it went, and where it came from (<c>console</c>, <c>player</c>, <c>panel</c>, or
/// null for a room chat command). Names are null when the player or room is gone.
/// </summary>
public sealed record CommandLogEntry(
    int Id,
    DateTime AtUtc,
    int PlayerId,
    string? PlayerName,
    int RoomId,
    string? RoomName,
    string Command,
    string Arguments,
    string Outcome,
    string? Source
);
