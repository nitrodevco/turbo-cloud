using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly HashSet<RoomObjectId> _freezeCancelsOnTeleport = [];

    /// <summary>Remembers whether a frozen avatar thaws when a wired teleport moves it.</summary>
    public void SetFreezeCancelsOnTeleport(RoomObjectId objectId, bool cancels)
    {
        if (cancels)
            _freezeCancelsOnTeleport.Add(objectId);
        else
            _freezeCancelsOnTeleport.Remove(objectId);
    }

    public bool FreezeCancelsOnTeleport(RoomObjectId objectId) =>
        _freezeCancelsOnTeleport.Contains(objectId);
}
