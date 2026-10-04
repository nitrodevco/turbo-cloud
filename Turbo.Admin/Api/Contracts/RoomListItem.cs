using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A room in the panel's search results.</summary>
public sealed record RoomListItem(
    int Id,
    string Name,
    int OwnerId,
    string OwnerName,
    string DoorMode,
    int PlayersMax,
    string? CategoryName,
    bool IsLoaded,
    int Population,
    DateTime LastActiveUtc
);
