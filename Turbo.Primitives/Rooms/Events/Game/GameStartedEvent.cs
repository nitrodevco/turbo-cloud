using Orleans;

namespace Turbo.Primitives.Rooms.Events.Game;

[GenerateSerializer]
public sealed record GameStartedEvent : RoomEvent;
