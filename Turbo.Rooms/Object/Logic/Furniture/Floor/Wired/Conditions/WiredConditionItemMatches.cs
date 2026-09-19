using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the picked furni stand as they did when the box was saved. The four params choose
/// which aspects count: state, direction, position, altitude.
/// </summary>
[RoomObjectLogic("wf_cnd_match_snapshot")]
public class WiredConditionItemMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.STATES_MATCH;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // state
            new WiredBoolParamRule(false), // direction
            new WiredBoolParamRule(false), // position
            new WiredBoolParamRule(false), // altitude
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems],
        ];

    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (!await base.ApplyWiredUpdateAsync(ctx, update, ct))
            return false;

        CaptureFurniSnapshot(GetStuffIds());

        return true;
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        DeleteFurniSnapshot();

        await base.OnPickupAsync(ctx, ct);
    }

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var snapshot = GetFurniSnapshot();

        if (snapshot.Count == 0)
            return false;

        var checkState = GetIntParamOrDefault(0, false);
        var checkRotation = GetIntParamOrDefault(1, false);
        var checkPosition = GetIntParamOrDefault(2, false);
        var checkAltitude = GetIntParamOrDefault(3, false);

        foreach (var (itemId, entry) in snapshot)
        {
            if (!TryGetFloorItem(itemId, out var item))
                return false;

            if (checkState && item.Logic.GetState() != entry.State)
                return false;

            if (checkRotation && (int)item.Rotation != entry.Rotation)
                return false;

            if (checkPosition && (item.X != entry.X || item.Y != entry.Y))
                return false;

            if (checkAltitude && item.Z.ToInt() != entry.Z)
                return false;
        }

        return true;
    }
}
