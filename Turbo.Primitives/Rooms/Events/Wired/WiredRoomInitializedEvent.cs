using Orleans;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// The room's wired started: the first wired tick after the room loaded (someone entered it while
/// it was not loaded, or it came back after a reload). Raised once per load, before any other event.
/// </summary>
[GenerateSerializer]
public sealed record WiredRoomInitializedEvent : RoomEvent;
