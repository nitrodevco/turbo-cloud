namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What changed in the hotel since the last message, for the panel to fetch again: the dashboard's
/// figures, rooms, players, and players' permissions, by id, and whether there is something new
/// for the bell. Only what the viewer may see.
/// </summary>
public sealed record LiveChangesMessage(
    bool Dashboard,
    int[] Rooms,
    int[] Players,
    int[] Permissions,
    bool Notifications
);
