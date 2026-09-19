using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Dresses a named bot in the figure string after the tab in the string param.</summary>
[RoomObjectLogic("wf_act_bot_clothes")]
public class WiredActionBotChangeFigure(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_CHANGE_FIGURE;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, figure) = SplitParam();

        if (!FigureString.IsWellFormed(figure) || !TryGetBot(botName, out var bot))
            return false;

        await _roomGrain.BotModule.SetFigureAsync(bot, figure, bot.Gender, ct);

        return true;
    }
}
