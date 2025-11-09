using Orleans;

namespace Turbo.Primitives.Orleans.Events.Rooms;

[GenerateSerializer, Immutable]
public sealed record PlayerJoinedEvent(long RoomId, long PlayerId) : RoomEvent(RoomId);
