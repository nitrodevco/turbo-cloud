using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Rooms.Events;

[GenerateSerializer]
public abstract record RoomEvent : IEvent
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public ActionContext CausedBy { get; init; } = ActionContext.Invalid;
}
