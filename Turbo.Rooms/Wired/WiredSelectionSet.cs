using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredSelectionSet : IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; } = [];
    public HashSet<int> SelectedAvatarIds { get; } = [];

    public bool HasFurni => SelectedFurniIds.Count > 0;
    public bool HasAvatars => SelectedAvatarIds.Count > 0;

    public void UnionWith(IWiredSelectionSet other)
    {
        SelectedFurniIds.UnionWith(other.SelectedFurniIds);
        SelectedAvatarIds.UnionWith(other.SelectedAvatarIds);
    }
}
