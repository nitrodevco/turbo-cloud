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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Runs the stacks the picked wired boxes belong to, handing them the current selection. The
/// target stack runs its actions when its own conditions pass; the negative variant runs them
/// when they fail. Depth is bounded by the room config.
/// </summary>
[RoomObjectLogic("wf_act_call_stacks")]
public class WiredActionCallStacks(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.CALL_ANOTHER_STACK;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        if (IsCallDepthExceeded(ctx))
            return false;

        var stackIds = new HashSet<int>();
        var ownStackId = _ctx.GetTileIdx();

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            var stackId = MapModule.ToIdx(item.X, item.Y);

            if (stackId != ownStackId && WiredSystem.HasStack(stackId))
                stackIds.Add(stackId);
        }

        if (stackIds.Count == 0)
            return false;

        var call = new WiredStackCalledEvent
        {
            RoomId = _roomGrain.RoomId,
            CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
            StackIds = [.. stackIds],
            FurniIds = [.. ctx.Selected.SelectedFurniIds],
            AvatarIds = [.. ctx.Selected.SelectedAvatarIds],
            Depth = ctx.Depth + 1,
            IsNegative = IsNegativeCall,
        };

        // The called stacks run in this execution's context.
        if (ctx is WiredContext caller)
            WiredSystem.CarryContextValues(call, caller.ContextValues);

        // And its placeholders, as they read now (variables-info #20).
        if (ctx is WiredExecutionContext texts)
            WiredSystem.CarryPlaceholders(call, await texts.ResolvePlaceholdersToCarryAsync(ct));

        await _ctx.PublishRoomEventAsync(call, ct);

        return true;
    }

    protected virtual bool IsNegativeCall => false;
}
