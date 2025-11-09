using Orleans;

namespace Turbo.Primitives.Orleans.Events.Rooms;

[GenerateSerializer]
public abstract record RoomEvent(long RoomId);
