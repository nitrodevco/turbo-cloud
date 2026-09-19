using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>A named bot walks to one of the picked furni; arrival raises the bot trigger.</summary>
[RoomObjectLogic("wf_act_bot_move")]
public class WiredActionBotMove(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_MOVE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, _) = SplitParam();

        if (!TryGetBot(botName, out var bot))
            return false;

        var items = GetFloorItems(ctx.GetSelection(this));

        if (items.Count == 0)
            return false;

        return await _roomGrain.BotModule.WalkToItemAsync(
            bot,
            items[Random.Shared.Next(items.Count)],
            ct
        );
    }
}
