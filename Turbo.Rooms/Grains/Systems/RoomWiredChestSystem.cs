using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// What concerns every wired chest in the room at once: a chest set to lock when its owner
/// leaves does so, and the wired menu locks or unlocks many in one go
/// (<c>wiredchests.lock_info.*</c>).
/// </summary>
public sealed class RoomWiredChestSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    public async Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not PlayerLeftEvent left)
            return;

        foreach (var chest in Chests())
        {
            // A window left open stays open in the client, but this room no longer hears its close.
            await chest.ForgetViewerAsync(left.PlayerId, ct);

            if (
                chest.AutoLocks
                && !chest.IsLocked
                && chest.Context.RoomObject.OwnerId == left.PlayerId
            )
                await chest.SetLockedAsync(true);
        }
    }

    /// <summary>
    /// A player looks inside one chest at a time: the client keeps a single chest window, which
    /// the next chest's contents take over, and only ever closes the chest it last opened
    /// (<c>WiredChestController.setClosedStatus</c>). So opening a chest closes any other the
    /// player had open, or that one would stay drawn open for good.
    /// </summary>
    public async Task CloseOtherChestsAsync(
        PlayerId playerId,
        FurnitureWiredChestLogic opened,
        CancellationToken ct
    )
    {
        foreach (var chest in Chests())
        {
            if (!ReferenceEquals(chest, opened))
                await chest.ForgetViewerAsync(playerId, ct);
        }
    }

    /// <summary>
    /// Locks or unlocks the player's own chests. Locking all of them, theirs or not, is the
    /// room owner's; only a chest's owner ever unlocks it.
    /// </summary>
    public async Task LockAsync(ActionContext ctx, bool locked, bool all)
    {
        var everyChest = locked && all;

        if (everyChest && !await SecurityModule.GetIsRoomOwnerAsync(ctx))
        {
            _roomGrain._logger.LogWarning(
                "Player {PlayerId} asked to lock every wired chest in room {RoomId} without owning it; refused",
                ctx.PlayerId,
                _roomGrain.RoomId
            );

            return;
        }

        foreach (var chest in Chests())
        {
            if (everyChest || chest.Context.RoomObject.OwnerId == ctx.PlayerId)
                await chest.SetLockedAsync(locked);
        }
    }

    /// <summary>The chests in the room; a copy, since locking one changes nothing it walks.</summary>
    public List<FurnitureWiredChestLogic> Chests() =>
        [.. FurniModule.Items.Select(x => x.Logic).OfType<FurnitureWiredChestLogic>()];
}
