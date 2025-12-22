using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger
{
    public bool Matches(RoomEvent @event);
}
