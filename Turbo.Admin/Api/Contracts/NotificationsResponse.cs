using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>What staff should know of lately, newest first: the panel's bell.</summary>
public sealed record NotificationsResponse(NotificationItem[] Items);

/// <summary>
/// One thing to know of. <paramref name="Id"/> stays the same for the same thing, so the panel
/// can tell which it has seen. <paramref name="Kind"/> is <c>availability</c> (maintenance or a
/// shutdown, coming or under way; <paramref name="AtUtc"/> is when it starts), <c>ban</c> or
/// <c>refusedCommand</c>; <paramref name="PlayerId"/> is the player it is about, when there is one.
/// </summary>
public sealed record NotificationItem(
    string Id,
    string Kind,
    DateTime AtUtc,
    string Title,
    string? Detail,
    int? PlayerId
);
