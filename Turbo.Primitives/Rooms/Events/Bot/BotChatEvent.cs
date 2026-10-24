using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Avatar;

namespace Turbo.Primitives.Rooms.Events.Bot;

/// <summary>
/// A bot said something to the room. Wired hears it as a user speaking: Says Keyword fires on it
/// with the bot as the triggering user. A player's line is a
/// <see cref="Player.PlayerChatEvent"/>, which the chat pipeline may still rewrite or cancel.
/// </summary>
[GenerateSerializer]
public sealed record BotChatEvent : AvatarEvent
{
    [Id(0)]
    public required RoomChatType ChatType { get; init; }

    [Id(1)]
    public required string Text { get; init; }
}
