using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One page of a player search, and how many players are online now.</summary>
public sealed record PlayerListResponse(
    int Total,
    int Page,
    int PageSize,
    int OnlineNow,
    IReadOnlyList<PlayerListItem> Players
);
