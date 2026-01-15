using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredProcessingContext : WiredContext, IWiredProcessingContext
{
    public required RoomEvent Event { get; init; }
    public required IWiredStack Stack { get; init; }
    public required IWiredTrigger Trigger { get; init; }
}
