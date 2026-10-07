using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player going into a room, and when.</summary>
public sealed record RoomVisitItem(
    int PlayerId,
    string PlayerName,
    int RoomId,
    string RoomName,
    DateTime EnteredUtc
);
