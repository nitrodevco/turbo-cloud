using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A private message: when, who sent it to whom, and what; a Habbicon sent instead of text has no
/// message and its <see cref="HabbiconId"/>. <see cref="Delivered"/> is false while it waits for
/// the friend to log in. Names are null for a player who is gone.
/// </summary>
public sealed record MessageLogEntry(
    int Id,
    DateTime AtUtc,
    int SenderId,
    string? SenderName,
    int ReceiverId,
    string? ReceiverName,
    string Message,
    int HabbiconId,
    bool Delivered
);

/// <summary>A run of private messages, newest first. Paged by message, not counted: there is no total.</summary>
public sealed record MessageLogResponse(
    int PageSize,
    bool HasOlder,
    bool HasNewer,
    MessageLogEntry[] Entries
);
