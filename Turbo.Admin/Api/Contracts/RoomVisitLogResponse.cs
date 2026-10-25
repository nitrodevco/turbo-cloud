namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page of the room visits log in <see cref="Order"/>, counted no further than the chat log is
/// (<see cref="TotalCapped"/> when there are more than <see cref="Total"/>).
/// </summary>
public sealed record RoomVisitLogResponse(
    int Total,
    bool TotalCapped,
    int Page,
    int PageSize,
    ListOrder Order,
    RoomVisitLogEntry[] Entries
);
