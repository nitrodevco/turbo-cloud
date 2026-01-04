using System.Collections.Generic;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredSelectionSet : IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; } = [];
    public HashSet<int> SelectedAvatarIds { get; } = [];

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
