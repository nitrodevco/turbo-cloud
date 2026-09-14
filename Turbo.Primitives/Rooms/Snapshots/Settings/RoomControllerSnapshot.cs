using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>
/// A player explicitly granted rights in a room. Owners and group-derived controllers are not
/// listed; only assigned rights are.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomControllerSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }
}
