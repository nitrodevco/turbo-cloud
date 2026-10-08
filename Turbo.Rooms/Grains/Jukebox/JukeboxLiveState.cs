using System.Collections.Generic;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Jukebox;

internal sealed class JukeboxLiveState
{
    public required RoomObjectId JukeboxId { get; init; }

    /// <summary>
    /// The disks held, in playing order. Each keeps its owner (who put it in, and who gets it
    /// back) and its song id in <see cref="FurnitureItemSnapshot.Extra"/>.
    /// </summary>
    public List<FurnitureItemSnapshot> Disks { get; } = [];
}
