using System.Collections.Generic;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed class WiredContext : IWiredContext
{
    public required IRoomGrain Room { get; init; }
    public required RoomEvent Event { get; init; }

    public required IWiredStack Stack { get; init; }
    public IWiredTrigger? Trigger { get; init; } = null;

    public List<RoomObjectId> SelectedItemIds { get; } = [];
    public List<RoomObjectId> SelectedAvatarIds { get; } = [];

    public Dictionary<string, object?> Variables { get; } = [];

    public IWiredPolicy Policy { get; } = new WiredPolicy();

    public int Depth { get; set; } = 0;
    public HashSet<int> VisitedStackIdxs { get; } = [];
}
