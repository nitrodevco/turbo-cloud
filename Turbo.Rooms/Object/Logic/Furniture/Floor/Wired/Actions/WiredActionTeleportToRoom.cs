using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sends the selected users to another room. The client offers no room input, so the target
/// comes from a picked room linker furni (its extra data section "room_linker" holds the room
/// id) or, failing that, from a numeric string param.
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
        [
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var roomId = ResolveRoomId(selection);

        if (roomId is null || roomId.Value == _roomGrain.RoomId)
            return false;

        var players = GetPlayers(selection);

        if (players.Count == 0)
            return false;

        foreach (var player in players)
        {
            await _roomGrain.SendComposerToPlayersAsync(
                [player.PlayerId],
                new RoomForwardMessageComposer { RoomId = roomId.Value },
                ct
            );
        }

        return true;
    }

    private RoomId? ResolveRoomId(IWiredSelectionSet selection)
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
                return RoomId.Parse(linker.RoomId);
        }

        return int.TryParse(_wiredData.StringParam, out var parsed) && parsed > 0
            ? RoomId.Parse(parsed)
            : null;
    }
}
