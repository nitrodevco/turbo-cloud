using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One moderation action, with the names of whoever it was by and to.</summary>
public sealed record ModerationActionEntry(
    long Id,
    DateTime AtUtc,
    string Action,
    string Source,
    int? ActorId,
    string? ActorName,
    int? TargetPlayerId,
    string? TargetName,
    int? RoomId,
    string? RoomName,
    int? GroupId,
    string? Reason,
    DateTime? ExpiresAtUtc,
    string? Detail
);

/// <summary>
/// A page of moderation actions, newest first. <see cref="Before"/> asks for the next page; null
/// when there is none.
/// </summary>
public sealed record ModerationActionsResponse(
    IReadOnlyList<ModerationActionEntry> Actions,
    long? Before
);

/// <summary>
/// One thing that happened to or was done by a player: <see cref="Kind"/> is moderation, login,
/// wallet, furni, room, command or chat.
/// </summary>
public sealed record PlayerTimelineEvent(
    DateTime AtUtc,
    string Kind,
    string Summary,
    int? RoomId = null,
    int? OtherPlayerId = null
);

/// <summary>
/// A player's history across the hotel's records, newest first. <see cref="Before"/> asks for
/// the next page; null when there is none.
/// </summary>
public sealed record PlayerTimelineResponse(
    IReadOnlyList<PlayerTimelineEvent> Events,
    DateTime? Before
);

/// <summary>A session the player opened. The address and machine only for staff who may see them.</summary>
public sealed record PlayerLoginEntry(
    DateTime AtUtc,
    DateTime? LoggedOutAtUtc,
    string? Address,
    string? Machine
);

/// <summary>
/// Another account that logged in from one of the player's addresses or machines: how many
/// addresses they share, whether a machine too (a far stronger sign), when they last did, and
/// whether that account is banned.
/// </summary>
public sealed record AltAccount(
    int PlayerId,
    string? Name,
    int SharedAddresses,
    bool SharedMachine,
    DateTime LastSharedAtUtc,
    bool Banned
);
