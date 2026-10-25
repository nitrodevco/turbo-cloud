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

/// <summary>
/// A page of private messages in <see cref="Order"/>, counted no further than the chat log is
/// (<see cref="TotalCapped"/> when there are more than <see cref="Total"/>).
/// </summary>
public sealed record MessageLogResponse(
    int Total,
    bool TotalCapped,
    int Page,
    int PageSize,
    ListOrder Order,
    MessageLogEntry[] Entries
);
