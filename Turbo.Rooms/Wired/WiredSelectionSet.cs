using System.Collections.Generic;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredSelectionSet : IWiredSelectionSet
{
    public HashSet<int> SelectedFurniIds { get; } = [];
    public HashSet<int> SelectedPlayerIds { get; } = [];

    public bool HasFurni => SelectedFurniIds.Count > 0;
    public bool HasPlayers => SelectedPlayerIds.Count > 0;

    public IWiredSelectionSet UnionWith(IWiredSelectionSet other)
    {
        SelectedFurniIds.UnionWith(other.SelectedFurniIds);
        SelectedPlayerIds.UnionWith(other.SelectedPlayerIds);

        return this;
    }

    public WiredSelectionSetSnapshot GetSnapshot() =>
        new()
        {
            SelectedFurniIds = [.. SelectedFurniIds],
            SelectedPlayerIds = [.. SelectedPlayerIds],
        };
}
