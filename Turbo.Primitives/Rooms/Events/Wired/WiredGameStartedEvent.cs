using Orleans;

namespace Turbo.Primitives.Rooms.Events.Wired;

[GenerateSerializer]
public sealed record WiredGameStartedEvent : RoomEvent;
