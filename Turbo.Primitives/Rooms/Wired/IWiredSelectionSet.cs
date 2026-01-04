using System.Collections.Generic;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; }
    public HashSet<int> SelectedAvatarIds { get; }

    public bool HasFurni { get; }
    public bool HasAvatars { get; }

    public IWiredSelectionSet UnionWith(IWiredSelectionSet other);
    public WiredSelectionSetSnapshot GetSnapshot();
}
