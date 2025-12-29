using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; }
    public HashSet<int> SelectedAvatarIds { get; }

    public bool HasFurni { get; }
    public bool HasAvatars { get; }

    public void UnionWith(IWiredSelectionSet other);
}
