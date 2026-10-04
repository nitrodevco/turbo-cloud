using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One page of a room search, and how many rooms matched in all.</summary>
public sealed record RoomListResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<RoomListItem> Rooms
);
