using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sends a signal to the antenna furni of the first slot; the furni of the second slot and
/// the selected users travel with it. Params: split by furni and split by users, each of
/// which sends one signal per forwarded furni or user, in a random order, instead of one for all.
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

    // The antennas, then the furni to forward; the forwarded furni and users default to the
    // selectors' (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    // The antennas slot takes antennas only (wiredfurni.error.require_antenna_furni; which box
    // the text is for is inferred from its wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot == 0 && !IsAntenna(item) ? WiredSaveErrors.REQUIRE_ANTENNA_FURNI : null;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var antennas = WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds;

        if (antennas.Count == 0)
            return false;

        var forwardedFurni = WiredSlotSelection.ForSlot(this, ctx, 1).SelectedFurniIds;
        var forwardedUsers = (ctx.GetSelection(this)).SelectedAvatarIds;
        var splitFurni = GetIntParamOrDefault(0, false);
        var splitUsers = GetIntParamOrDefault(1, false);

        var furniBatches =
            splitFurni && forwardedFurni.Count > 0
                ? Split(forwardedFurni)
                : [new HashSet<int>(forwardedFurni)];
        var userBatches =
            splitUsers && forwardedUsers.Count > 0
                ? Split(forwardedUsers)
                : [new HashSet<RoomObjectId>(forwardedUsers)];

        foreach (var furniBatch in furniBatches)
        {
            foreach (var userBatch in userBatches)
            {
                var signal = new WiredSignalEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                    AntennaIds = new HashSet<int>(antennas),
                    FurniIds = furniBatch,
                    AvatarIds = userBatch,
                    Depth = ctx.Depth + 1,
                    SenderId = ObjectId,
                    TriggererIds = ctx is WiredContext fired
                        ? [.. fired.Selected.SelectedAvatarIds]
                        : [],
                };

                // The receiving stacks start from this execution's context variable values.
                if (ctx is WiredContext sender)
                    WiredSystem.CarryContextValues(signal, sender.ContextValues);

                // And its placeholders, as they read now (variables-info #20).
                if (ctx is WiredExecutionContext texts)
                    WiredSystem.CarryPlaceholders(
                        signal,
                        await texts.ResolvePlaceholdersToCarryAsync(ct)
                    );

                await _ctx.PublishRoomEventAsync(signal, ct);
            }
        }

        return true;
    }

    /// <summary>
    /// One batch per id, in a random order: Habbo's split sends do not arrive in the order the
    /// furni or users were picked, so builds number them as they come in.
    /// </summary>
    private static List<HashSet<TId>> Split<TId>(HashSet<TId> ids)
    {
        var order = new TId[ids.Count];

        ids.CopyTo(order);
        Random.Shared.Shuffle(order);

        var batches = new List<HashSet<TId>>(order.Length);

        foreach (var id in order)
            batches.Add([id]);

        return batches;
    }
}
