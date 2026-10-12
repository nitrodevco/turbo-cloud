using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Puts furni back the way they were when the box was saved. The four params pick what to
/// restore: state, direction, position, altitude. The furni come from the box's source (picked,
/// triggered, selector or signal, as Habbo offers it: allowedFurniSources [[0, 100, 200, 201]]);
/// furni in the snapshot get their own entry, and when the snapshot holds a single furni, every
/// other furni takes that furni's state (recorded on hhus, WIN63-202610121709: one tile
/// snapshotted in state 3 turns every tile a selector picks to 3).
/// </summary>
[RoomObjectLogic("wf_act_match_to_sshot")]
public class WiredActionMatchToSnapshot(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SET_FURNI_STATE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // state
            new WiredBoolParamRule(false), // direction
            new WiredBoolParamRule(false), // position
            new WiredBoolParamRule(false), // altitude
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    protected override bool KeepsFurniSnapshot => true;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var snapshot = GetFurniSnapshot();
        var restoreState = GetIntParamOrDefault(0, false);
        var restoreRotation = GetIntParamOrDefault(1, false);
        var restorePosition = GetIntParamOrDefault(2, false);
        var restoreAltitude = GetIntParamOrDefault(3, false);

        // A furni outside a one-furni snapshot takes that furni's state; nothing else of it was
        // recorded, so direction, position and altitude stay with furni of the snapshot itself.
        WiredFurniSnapshotEntry? onlyEntry = snapshot.Count == 1 ? snapshot.Values.First() : null;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (!snapshot.TryGetValue(item.ObjectId, out var entry))
            {
                if (restoreState && onlyEntry is { } only && item.Logic.GetState() != only.State)
                    await ctx.ProcessItemStateUpdateAsync(item, only.State);

                continue;
            }

            if (restoreState && item.Logic.GetState() != entry.State)
                await ctx.ProcessItemStateUpdateAsync(item, entry.State);

            var targetX = restorePosition ? entry.X : item.X;
            var targetY = restorePosition ? entry.Y : item.Y;
            var targetRotation = restoreRotation ? (Rotation)entry.Rotation : item.Rotation;
            Altitude? targetZ = restoreAltitude ? Altitude.FromInt(entry.Z) : null;

            if (
                targetX == item.X
                && targetY == item.Y
                && targetRotation == item.Rotation
                && (targetZ is null || targetZ == item.Z)
            )
                continue;

            await ctx.TryMoveFloorItemAsync(item, targetX, targetY, targetZ, targetRotation);
        }

        return true;
    }
}
