using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>
/// What the room settings window edits: the room's own snapshot plus the hotel-wide limits the
/// client needs to validate the form.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomSettingsSnapshot
{
    [Id(0)]
    public required RoomSnapshot Room { get; init; }

    [Id(1)]
    public required int MaximumVisitorsLimit { get; init; }
}
