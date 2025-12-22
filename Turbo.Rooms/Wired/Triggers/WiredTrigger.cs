using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Triggers;

public abstract class WiredTrigger : IWiredTrigger
{
    public abstract bool Matches(RoomEvent @event);
}
