using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sends a signal to the antenna furni of the first slot; the furni of the second slot and
/// the selected users travel with it. Params: split by furni and split by users, each of
/// which sends one signal per forwarded furni or user instead of one for all.
/// </summary>
[RoomObjectLogic("wf_act_send_signal")]
public class WiredActionSendSignal(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SEND_SIGNAL;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredBoolParamRule(false), new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.Depth >= _roomGrain._wiredConfig.MaxDepth)
        {
            _roomGrain.WiredSystem.RecordError(
                "WiredCallDepthExceeded",
                Grains.Systems.RoomWiredSystem.GetErrorCategory(this),
                _roomGrain.NowMs()
            );

            return false;
        }

        var antennas = WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds;

        if (antennas.Count == 0)
            return false;

        var forwardedFurni = WiredSlotSelection.ForSlot(this, ctx, 1).SelectedFurniIds;
        var forwardedUsers = (ctx.GetSelection(this)).SelectedPlayerIds;
        var splitFurni = GetIntParamOrDefault(0, false);
        var splitUsers = GetIntParamOrDefault(1, false);

        var furniBatches =
            splitFurni && forwardedFurni.Count > 0
                ? Split(forwardedFurni)
                : [new HashSet<int>(forwardedFurni)];
        var userBatches =
            splitUsers && forwardedUsers.Count > 0
                ? Split(forwardedUsers)
                : [new HashSet<int>(forwardedUsers)];

        foreach (var furniBatch in furniBatches)
        {
            foreach (var userBatch in userBatches)
            {
                await _ctx.PublishRoomEventAsync(
                    new WiredSignalEvent
                    {
                        RoomId = _roomGrain.RoomId,
                        CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                        AntennaIds = new HashSet<int>(antennas),
                        FurniIds = furniBatch,
                        PlayerIds = userBatch,
                        Depth = ctx.Depth + 1,
                        SenderId = ObjectId,
                    },
                    ct
                );
            }
        }

        return true;
    }

    private static List<HashSet<int>> Split(HashSet<int> ids)
    {
        var batches = new List<HashSet<int>>(ids.Count);

        foreach (var id in ids)
            batches.Add([id]);

        return batches;
    }
}
