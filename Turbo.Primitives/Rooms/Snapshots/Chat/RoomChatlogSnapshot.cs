using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots.Chat;

[GenerateSerializer, Immutable]
public sealed record RoomChatlogSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }

    [Id(2)]
    public PlayerId? TargetPlayerId { get; init; }

    [Id(3)]
    public required string Text { get; init; }
}
