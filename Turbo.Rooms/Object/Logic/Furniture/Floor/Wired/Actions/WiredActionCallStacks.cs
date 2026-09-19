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
        if (ctx.Depth >= _roomGrain._wiredConfig.MaxDepth)
        {
            _roomGrain.WiredSystem.RecordError(
                "WiredCallDepthExceeded",
                Grains.Systems.RoomWiredSystem.GetErrorCategory(this),
                _roomGrain.NowMs()
            );

            return false;
        }

        var stackIds = new HashSet<int>();
        var ownStackId = _ctx.GetTileIdx();

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            var stackId = _roomGrain.MapModule.ToIdx(item.X, item.Y);

            if (stackId != ownStackId && _roomGrain.WiredSystem.HasStack(stackId))
                stackIds.Add(stackId);
        }

        if (stackIds.Count == 0)
            return false;

        await _ctx.PublishRoomEventAsync(
            new WiredStackCalledEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                StackIds = [.. stackIds],
                FurniIds = [.. ctx.Selected.SelectedFurniIds],
                PlayerIds = [.. ctx.Selected.SelectedPlayerIds],
                Depth = ctx.Depth + 1,
                IsNegative = IsNegativeCall,
            },
            ct
        );

        return true;
    }

    protected virtual bool IsNegativeCall => false;
}
