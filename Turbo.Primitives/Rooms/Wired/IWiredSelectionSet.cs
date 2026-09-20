using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; }

    /// <summary>
    /// The avatars a stack has picked, by room index. Players, pets and bots are all in here:
    /// wired calls them users, and a box that only makes sense for one kind narrows it down
    /// itself (as the "users by type" selector does).
    /// </summary>
    public HashSet<RoomObjectId> SelectedAvatarIds { get; }

    public bool HasFurni { get; }
    public bool HasAvatars { get; }

    public IWiredSelectionSet UnionWith(IWiredSelectionSet other);
    public WiredSelectionSetSnapshot GetSnapshot();
}
