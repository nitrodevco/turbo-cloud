using System.Collections.Generic;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredContext
{
    public RoomEvent Event { get; }
    public List<RoomObjectId> SelectedItemIds { get; }
    public List<RoomObjectId> SelectedAvatarIds { get; }
    public Dictionary<string, object?> Variables { get; }
    public IWiredPolicy Policy { get; }
    public int Depth { get; set; }
    public HashSet<int> VisitedStackIdxs { get; }
}
