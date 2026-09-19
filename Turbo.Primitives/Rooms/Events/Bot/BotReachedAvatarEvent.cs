using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Bot;

/// <summary>A bot walking after an avatar arrived next to it.</summary>
[GenerateSerializer]
public sealed record BotReachedAvatarEvent : RoomEvent
{
    [Id(0)]
    public required RoomObjectId BotObjectId { get; init; }

    [Id(1)]
    public required string BotName { get; init; }

    [Id(2)]
    public required RoomObjectId TargetObjectId { get; init; }
}
