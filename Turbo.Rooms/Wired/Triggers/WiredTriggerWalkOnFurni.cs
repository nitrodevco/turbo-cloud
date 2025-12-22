using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Triggers;

[WiredDefinition("wf_trg_walks_on_furni")]
public class WiredTriggerWalkOnFurni : WiredTrigger
{
    public override bool Matches(RoomEvent @event)
    {
        return @event is AvatarWalkOnFurniEvent;
    }
}
