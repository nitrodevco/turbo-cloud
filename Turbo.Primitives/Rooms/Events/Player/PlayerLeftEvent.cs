using Orleans;

namespace Turbo.Primitives.Rooms.Events.Player;

[GenerateSerializer]
public sealed record PlayerLeftEvent : PlayerEvent;
