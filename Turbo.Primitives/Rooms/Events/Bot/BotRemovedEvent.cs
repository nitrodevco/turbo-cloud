using Orleans;
using Turbo.Primitives.Rooms.Events.Avatar;

namespace Turbo.Primitives.Rooms.Events.Bot;

/// <summary>
/// A bot was picked up from the room. Wired hears it as a user leaving. The bot is already gone,
/// so only the avatar it was is known.
/// </summary>
[GenerateSerializer]
public sealed record BotRemovedEvent : AvatarEvent;
