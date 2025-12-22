using System.Collections.Generic;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

internal sealed class WiredContext : IWiredContext
{
    public required RoomGrain Room { get; init; }
    public required RoomEvent Event { get; init; }

    public List<RoomObjectId> SelectedItemIds { get; } = [];
    public List<RoomObjectId> SelectedAvatarIds { get; } = [];

    public Dictionary<string, object?> Variables { get; } = [];

    public IWiredPolicy Policy { get; } = new WiredPolicy();

    public int Depth { get; set; } = 0;
    public HashSet<int> VisitedStackIdxs { get; } = [];
}
