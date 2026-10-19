namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What changed in the hotel since the last message, for the panel to fetch again: the dashboard's
/// figures, rooms, players, and players' permissions, by id, whether there is something new
/// for the bell, and whether calls for help changed. Only what the viewer may see.
/// </summary>
public sealed record LiveChangesMessage(
    bool Dashboard,
    int[] Rooms,
    int[] Players,
    int[] Permissions,
    bool Notifications,
    bool Reports
);
