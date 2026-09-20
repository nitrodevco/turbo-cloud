using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sends the selected users to another room. The client offers no room input, so the target
/// comes from a picked furni carrying a <see cref="RoomLinkerData"/> section: a room linker names
/// the room, a teleporter names its pair and leads to wherever that pair stands now. Failing
/// that, a numeric string param names the room.
/// </summary>
[RoomObjectLogic("wf_act_teleport_to_room")]
public class WiredActionTeleportToRoom(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.TELEPORT_TO_ROOM;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var destination = await ResolveDestinationAsync(selection, ct);

        if (destination is not var (roomId, entry) || roomId == _roomGrain.RoomId)
            return false;

        var players = GetPlayers(selection);

        if (players.Count == 0)
            return false;

        foreach (var player in players)
        {
            // How they are arriving is told first: the forward is what makes the client ask to
            // enter, and the room reads the entry as they land.
            await _roomGrain
                ._grainFactory.GetPlayerPresenceGrain(player.PlayerId)
                .SetPendingRoomEntryAsync(roomId, entry, ct);

            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                player.PlayerId,
                new RoomForwardMessageComposer { RoomId = roomId },
                ct
            );
        }

        return true;
    }

    /// <summary>
    /// Where the picked furni leads and how arriving there counts. A furni naming one fixed
    /// room is a room network; one naming the other half of a pair is a teleporter, and the
    /// player arrives at that half. A room named by the box's text alone is a plain entry,
    /// because no furni took them there.
    /// </summary>
    private async Task<(RoomId RoomId, RoomEntrySnapshot Entry)?> ResolveDestinationAsync(
        IWiredSelectionSet selection,
        CancellationToken ct
    )
    {
        foreach (var item in GetFloorItems(selection))
        {
            var linker = FurnitureExtraDataSections.Read<RoomLinkerData>(
                item.ExtraData,
                item.Definition.ExtraData,
                RoomLinkerData.SECTION,
                _roomGrain._logger
            );

            if (linker is { RoomId: > 0 })
                return (
                    RoomId.Parse(linker.RoomId),
                    new RoomEntrySnapshot
                    {
                        Method = RoomEntryMethodType.RoomNetwork,
                        TeleportId = 0,
                    }
                );

            if (linker is { ItemId: > 0 })
            {
                var pairedRoomId = await _roomGrain.FurniModule.GetRoomIdOfItemAsync(
                    linker.ItemId,
                    ct
                );

                if (pairedRoomId is not null)
                    return (
                        pairedRoomId.Value,
                        new RoomEntrySnapshot
                        {
                            Method = RoomEntryMethodType.Teleport,
                            TeleportId = linker.ItemId,
                        }
                    );
            }
        }

        return int.TryParse(_wiredData.StringParam, out var parsed) && parsed > 0
            ? (RoomId.Parse(parsed), RoomEntrySnapshot.Default)
            : null;
    }
}
