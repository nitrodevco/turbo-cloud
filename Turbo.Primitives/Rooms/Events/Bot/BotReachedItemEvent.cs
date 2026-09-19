using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Bot;

/// <summary>A bot sent to a furni by wired arrived on its tile.</summary>
[GenerateSerializer]
public sealed record BotReachedItemEvent : RoomEvent
{
    [Id(0)]
    public required RoomObjectId BotObjectId { get; init; }

    [Id(1)]
    public required string BotName { get; init; }

    [Id(2)]
    public required RoomObjectId FurniId { get; init; }
}
