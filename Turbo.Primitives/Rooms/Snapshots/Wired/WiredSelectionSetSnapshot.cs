using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredSelectionSetSnapshot
{
    [Id(0)]
    public required HashSet<int> SelectedFurniIds { get; init; }

    [Id(1)]
    public required HashSet<int> SelectedAvatarIds { get; init; }
}
