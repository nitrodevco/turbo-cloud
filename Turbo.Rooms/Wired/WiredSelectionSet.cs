using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredSelectionSet : IWiredSelectionSet
{
    public WiredSelectionSet() { }

    public WiredSelectionSet(IEnumerable<int> furniIds, IEnumerable<RoomObjectId> avatarIds)
    {
        SelectedFurniIds.UnionWith(furniIds);
        SelectedAvatarIds.UnionWith(avatarIds);
    }

    public HashSet<int> SelectedFurniIds { get; } = [];
    public HashSet<RoomObjectId> SelectedAvatarIds { get; } = [];

    public bool HasFurni => SelectedFurniIds.Count > 0;
    public bool HasAvatars => SelectedAvatarIds.Count > 0;

    public IWiredSelectionSet UnionWith(IWiredSelectionSet other)
    {
        SelectedFurniIds.UnionWith(other.SelectedFurniIds);
        SelectedAvatarIds.UnionWith(other.SelectedAvatarIds);

        return this;
    }

    public WiredSelectionSetSnapshot GetSnapshot() =>
        new()
        {
            SelectedFurniIds = [.. SelectedFurniIds],
            SelectedAvatarIds = [.. SelectedAvatarIds],
        };
}
