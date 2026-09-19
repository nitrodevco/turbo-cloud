using Orleans;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A variable fx addon was placed, moved, picked up or saved: what it looks like, or which
/// variable box it shares a tile with, may have changed.
/// </summary>
[GenerateSerializer]
public sealed record WiredVariableFxBoxChangedEvent : RoomEvent;
