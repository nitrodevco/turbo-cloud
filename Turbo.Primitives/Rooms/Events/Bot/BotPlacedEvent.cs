using Orleans;
using Turbo.Primitives.Rooms.Events.Avatar;

namespace Turbo.Primitives.Rooms.Events.Bot;

/// <summary>
/// A bot was put down in the room. Wired hears it as a user entering, with the bot as the
/// triggering user. A bot the room loads with does not enter.
/// </summary>
[GenerateSerializer]
public sealed record BotPlacedEvent : AvatarEvent
{
    [Id(0)]
    public required string BotName { get; init; }
}
