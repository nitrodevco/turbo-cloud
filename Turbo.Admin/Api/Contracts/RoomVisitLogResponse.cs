namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A run of the room visits log, newest first, with whether there are older and newer visits past
/// it. Paged by visit rather than counted, so a long log is never read whole to show one page.
/// </summary>
public sealed record RoomVisitLogResponse(
    int PageSize,
    bool HasOlder,
    bool HasNewer,
    RoomVisitLogEntry[] Entries
);
